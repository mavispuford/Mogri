using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using MobileDiffusion.Clients.SdForgeNeo;
using MobileDiffusion.Clients.SdForgeNeo.Models;
using MobileDiffusion.Clients.SdForgeNeo.Sdapi.V1.Options;
using MobileDiffusion.ViewModels;
using MobileDiffusion.Helpers;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Dynamic;
using MobileDiffusion.Models.Automatic1111;
using System.Text.RegularExpressions;

namespace MobileDiffusion.Services
{
    public class SdForgeNeoService : IImageGenerationService
    {
        private static class PngInfoProperties
        {
            public const string Steps = "steps";
            public const string Sampler = "sampler";
            public const string CfgScale = "cfg scale";
            public const string Seed = "seed";
            public const string Size = "size";
            public const string ModelHash = "model hash";
            public const string Model = "model";
            public const string LoraHashes = "lora hashes";
            public const string DenoisingStrength = "denoising strength";
            public const string Eta = "eta";
            public const string Version = "version";
            public const string HiresUpscaler = "hires upscaler";
            public const string HiresUpscale = "hires upscale";
            public const string HiresSteps = "hires steps";
            public const string Scheduler = "scheduler";
            public const string ScheduleType = "schedule type";
            public const string DistilledCfgScale = "distilled cfg scale";
            public const string DistilledCfgScaleKey = "distilled_cfg_scale";
            public const string Shift = "shift";
        }

        private Regex _loraRegex = new Regex("<lora:([^:]*):([^>]*)>", RegexOptions.Compiled);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceProvider _serviceProvider;
        private SdForgeNeoClient _client;
        private SdForgeNeoClient _progressClient;
        private string _baseUrl;
        private CancellationTokenSource _mainRequestCancellationSource;
        private Task _initializeTask;

        private List<SamplerItem> _samplers;
        private List<SchedulerItem> _schedulers;
        private List<PromptStyleItem> _promptStyles;
        private List<SDModelItem> _models;
        private List<LoraItem> _loras;
        private List<UpscalerItem> _upscalers;
        private Options _options;

        public bool Initialized { get; private set; }

        public SdForgeNeoService(IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider)
        {
            _httpClientFactory = httpClientFactory;
            _serviceProvider = serviceProvider;
        }

        public async Task InitializeAsync()
        {
             _baseUrl = Preferences.Default.Get(Constants.PreferenceKeys.ServerUrl, string.Empty);

            if (string.IsNullOrEmpty(_baseUrl) || !_baseUrl.Contains("http"))
            {
                Initialized = false;
                return;
            }
            
            // Initialize client
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_baseUrl);
            httpClient.Timeout = TimeSpan.FromMinutes(15);
            
            // Kiota setup
            var authProvider = new AnonymousAuthenticationProvider();
            var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);
            adapter.BaseUrl = _baseUrl;
            _client = new SdForgeNeoClient(adapter);

            // Initialize progress client
            var progressHttpClient = _httpClientFactory.CreateClient();
            progressHttpClient.BaseAddress = new Uri(_baseUrl);
            progressHttpClient.Timeout = TimeSpan.FromSeconds(10);

            var progressAdapter = new HttpClientRequestAdapter(authProvider, httpClient: progressHttpClient);
            progressAdapter.BaseUrl = _baseUrl;
            _progressClient = new SdForgeNeoClient(progressAdapter);

            if (_initializeTask == null || _initializeTask.Status != TaskStatus.Running)
            {
                _initializeTask = Task.Run(async () =>
                {
                    await RefreshResourcesAsync();
                });
            }

            await _initializeTask;
            _initializeTask = null;

            Initialized = true;
        }

        public async Task<bool> CheckServerAsync()
        {
            if (_client == null)
            {
                await InitializeAsync();
                if (_client == null) return false;
            }

            try
            {
                await _client.Sdapi.V1.OptionsPath.GetAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async IAsyncEnumerable<ApiResponse> SubmitImageRequestAsync(PromptSettings settings)
        {
            if (settings is null)
            {
                yield break;
            }

            if (string.IsNullOrEmpty(settings.InitImage))
            {
                // No image - Just do txt2img
                await foreach (ApiResponse apiResponse in sendTextToImageRequest(settings))
                {
                    yield return apiResponse;
                }
            }
            else
            {
                // Image included - Do img2img
                await foreach (ApiResponse apiResponse in sendImageToImageRequest(settings))
                {
                    yield return apiResponse;
                }
            }
        }

        private async IAsyncEnumerable<ApiResponse> sendTextToImageRequest(PromptSettings settings)
        {
            var request = txt2ImageRequestFromSettings(settings);
            
            if (_mainRequestCancellationSource?.IsCancellationRequested == false)
            {
                _mainRequestCancellationSource.Cancel();
            }

            _mainRequestCancellationSource = new CancellationTokenSource();
            var progressCancellationTokenSource = new CancellationTokenSource();
            var progressToken = progressCancellationTokenSource.Token;

            TextToImageResponse txt2ImgResponse = null;

            var textToImageTask = Task.Run(async () =>
            {
                try
                {
                    txt2ImgResponse = await _client.Sdapi.V1.Txt2img.PostAsync(request, cancellationToken: _mainRequestCancellationSource.Token);
                }
                finally
                {
                    _mainRequestCancellationSource = null;
                    progressCancellationTokenSource.Cancel();
                }
            });

            ApiResponse apiResponse = null;
            var skipCurrentImage = false;
            var finished = false;

            while (!finished)
            {
                try
                {
                    apiResponse = await getCurrentProgress(progressToken, skipCurrentImage);
                }
                catch (OperationCanceledException)
                {
                    await textToImageTask;

                    // Set the final response
                    var generationResponse = new GenerationResponse
                    {
                        Images = txt2ImgResponse.Images,
                        Info = txt2ImgResponse.Info,
                        Parameters = txt2ImgResponse.Parameters
                    };

                    apiResponse = new ApiResponse
                    {
                        StableDiffusionApi = Enums.StableDiffusionApi.Automatic1111,
                        ResponseObject = generationResponse,
                        Progress = 1f
                    };

                    finished = true;
                }

                yield return apiResponse;

                // Skip current image every other time
                skipCurrentImage = !skipCurrentImage;
            }
        }

        private async IAsyncEnumerable<ApiResponse> sendImageToImageRequest(PromptSettings settings)
        {
            var request = image2ImageRequestFromSettings(settings);

            if (_mainRequestCancellationSource?.IsCancellationRequested == false)
            {
                _mainRequestCancellationSource.Cancel();
            }

            _mainRequestCancellationSource = new CancellationTokenSource();
            var progressCancellationTokenSource = new CancellationTokenSource();
            var progressToken = progressCancellationTokenSource.Token;

            ImageToImageResponse img2ImgResponse = null;

            var imgToImageTask = Task.Run(async () =>
            {
                try
                {
                    img2ImgResponse = await _client.Sdapi.V1.Img2img.PostAsync(request, cancellationToken: _mainRequestCancellationSource.Token);
                }
                finally
                {
                    _mainRequestCancellationSource = null;
                    progressCancellationTokenSource.Cancel();
                }
            });

            ApiResponse apiResponse = null;
            var skipCurrentImage = true;
            var finished = false;

            while (!finished)
            {
                try
                {
                    apiResponse = await getCurrentProgress(progressToken, skipCurrentImage);
                }
                catch (OperationCanceledException)
                {
                    await imgToImageTask;

                    // Set the final response
                    var generationResponse = new GenerationResponse
                    {
                        Images = img2ImgResponse.Images,
                        Info = img2ImgResponse.Info,
                        Parameters = img2ImgResponse.Parameters
                    };

                    apiResponse = new ApiResponse
                    {
                        StableDiffusionApi = Enums.StableDiffusionApi.Automatic1111,
                        ResponseObject = generationResponse,
                        Progress = 1f
                    };

                    finished = true;
                }

                yield return apiResponse;

                // Skip current image every other time
                // skipCurrentImage = !skipCurrentImage;
            }
        }

        private async Task<ApiResponse> getCurrentProgress(CancellationToken token, bool skipCurrentImage)
        {
            return await Task.Run(async () =>
            {
                await Task.Delay(500, token);

                token.ThrowIfCancellationRequested();

                // Note: Kiota client doesn't support query parameters easily in the generated method signature if not exposed.
                // The generated method is: GetAsync(Action<RequestConfiguration<DefaultQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
                // We need to set skip_current_image query param.
                
                var progressGetResponse = await _progressClient.Sdapi.V1.Progress.GetAsync((config) => {
                    config.QueryParameters.SkipCurrentImage = skipCurrentImage;
                }, token);

                var progress = progressGetResponse.EtaRelative > 0 ? progressGetResponse.Progress : 1d;

                var progressResponse = new ProgressResponse
                {
                    Progress = progressGetResponse.Progress ?? 0,
                    EtaRelative = progressGetResponse.EtaRelative ?? 0,
                    CurrentImage = progressGetResponse.CurrentImage
                };

                var progressApiResponse = new ApiResponse
                {
                    StableDiffusionApi = Enums.StableDiffusionApi.Automatic1111,
                    ResponseObject = progressResponse,
                    Progress = progress.Value
                };

                return progressApiResponse;
            });
        }

        private StableDiffusionProcessingTxt2Img txt2ImageRequestFromSettings(PromptSettings settings)
        {
            var request = new StableDiffusionProcessingTxt2Img();

            request.NIter = settings.BatchCount;
            request.BatchSize = settings.BatchSize;
            request.CfgScale = settings.GuidanceScale;
            request.RestoreFaces = settings.EnableGfpgan;
            request.Width = (int)settings.Width;
            request.Height = (int)settings.Height;
            request.DenoisingStrength = settings.DenoisingStrength;
            request.Steps = settings.Steps;
            request.Seed = settings.Seed;
            request.Tiling = settings.Seamless == Enums.OnOff.on;

            // Hires Fix
            request.EnableHr = settings.EnableUpscaling &&
                !string.IsNullOrEmpty(settings.Upscaler) &&
                settings.UpscaleLevel > 0 &&
                settings.UpscaleSteps > 0;
            request.HrScale = settings.UpscaleLevel;
            request.HrSecondPassSteps = settings.UpscaleSteps;
            request.HrUpscaler = settings.Upscaler;

            var combinedPromptAndStyles = settings.GetCombinedPromptAndPromptStyles();
            request.Prompt = combinedPromptAndStyles.Prompt;
            request.NegativePrompt = combinedPromptAndStyles.NegativePrompt;

            request.SamplerName = settings.Sampler;

            System.Diagnostics.Debug.WriteLine($"[SdForgeNeoService] Generating image with ModelType: {settings.ModelType}");
            System.Diagnostics.Debug.WriteLine($"[SdForgeNeoService] Model: {settings.Model?.DisplayName}");
            System.Diagnostics.Debug.WriteLine($"[SdForgeNeoService] Steps: {settings.Steps}, CFG: {settings.GuidanceScale}, Sampler: {settings.Sampler}");

            if (settings.ModelType == Enums.ModelType.ZImage)
            {
                request.Scheduler = settings.Scheduler;
                System.Diagnostics.Debug.WriteLine($"[SdForgeNeoService] ZImage Scheduler: {settings.Scheduler}");
                
                if (settings.DistilledCfgScale.HasValue)
                {
                    request.AdditionalData.Add("distilled_cfg_scale", settings.DistilledCfgScale.Value);
                    System.Diagnostics.Debug.WriteLine($"[SdForgeNeoService] ZImage DistilledCfgScale: {settings.DistilledCfgScale.Value}");
                }
            }

            foreach (var lora in settings.Loras)
            {
                request.Prompt += $" <lora:{lora.Name}:{lora.Strength:F1}>";
            }

            return request;
        }

        private StableDiffusionProcessingImg2Img image2ImageRequestFromSettings(PromptSettings settings)
        {
            var request = new StableDiffusionProcessingImg2Img();

            request.NIter = settings.BatchCount;
            request.BatchSize = settings.BatchSize;
            request.CfgScale = settings.GuidanceScale;
            request.RestoreFaces = settings.EnableGfpgan;
            request.Width = (int)settings.Width;
            request.Height = (int)settings.Height;
            request.DenoisingStrength = settings.DenoisingStrength;
            request.Steps = settings.Steps;
            request.Seed = settings.Seed;
            request.Tiling = settings.Seamless == Enums.OnOff.on;

            var combinedPromptAndStyles = settings.GetCombinedPromptAndPromptStyles();
            request.Prompt = combinedPromptAndStyles.Prompt;
            request.NegativePrompt = combinedPromptAndStyles.NegativePrompt;

            request.SamplerName = settings.Sampler;
            
            var initImagesList = new List<UntypedNode>
            {
                new UntypedString(settings.InitImage)
            };
            request.InitImages = new UntypedArray(initImagesList);
            
            request.Mask = !string.IsNullOrEmpty(settings.Mask) ? settings.Mask : null;
            request.InpaintingFill = 1;

            // Because we colorize the image, blurring the mask would cause some of the colorized pixels to stay
            request.MaskBlur = 0;
            request.MaskBlurX = 0;
            request.MaskBlurY = 0;
            request.MaskRound = false;

            if (settings.ModelType == Enums.ModelType.ZImage)
            {
                request.Scheduler = settings.Scheduler;

                if (settings.DistilledCfgScale.HasValue)
                {
                    request.OverrideSettings = new StableDiffusionProcessingImg2Img_override_settings();
                    request.OverrideSettings.AdditionalData.Add("distilled_cfg_scale", settings.DistilledCfgScale.Value);
                }
            }

            foreach (var lora in settings.Loras)
            {
                request.Prompt += $" <lora:{lora.Name}:{lora.Strength:F1}>";
            }

            return request;
        }

        public Task<byte[]> GetImageBytesAsync(string url) => throw new NotImplementedException();
        public async Task<PromptSettings> GetImageInfoAsync(string base64EncodedImage)
        {
            var request = new PNGInfoRequest()
            {
                Image = base64EncodedImage
            };

            var imageInfoResult = await _client.Sdapi.V1.PngInfo.PostAsync(request);

            if (string.IsNullOrEmpty(imageInfoResult.Info))
            {
                return null;
            }

            var newLineSplit = imageInfoResult.Info.Split('\n');

            if (newLineSplit.Length <= 1)
            {
                return null;
            }

            var commaSplit = newLineSplit.LastOrDefault().Split(',');

            var properties = new Dictionary<string, string>();

            foreach (var item in commaSplit)
            {
                var itemSplit = item.Trim().Split(": ");

                if (itemSplit.Length == 2)
                {
                    properties.TryAdd(itemSplit.First(), itemSplit.Last());
                }
            }

            var prompt = newLineSplit[0];

            var loraMatches = _loraRegex.Matches(prompt);
            var loras = new List<LoraViewModel>();

            foreach (Match match in loraMatches)
            {
                var name = match.Groups[1].Value;

                if (loras.Any(l => l.Name == name))
                {
                    continue;
                }

                var lora = new LoraViewModel
                {
                    Name = name,
                    Strength = float.Parse(match.Groups[2].Value),
                };

                loras.Add(lora);

                prompt = prompt.Replace(match.Groups[0].Value, string.Empty);
            }

            var negativePrompt = newLineSplit.Length > 2 ? newLineSplit[1].Trim().Split(": ").Last() : string.Empty;

            var settings = new PromptSettings
            {
                Prompt = prompt,
                NegativePrompt = negativePrompt,
                Loras = loras
            };

            foreach (var property in properties)
            {
                try
                {
                    switch (property.Key.ToLower())
                    {
                        case PngInfoProperties.Steps:
                            settings.Steps = int.Parse(property.Value);
                            break;
                        case PngInfoProperties.Sampler:
                            settings.Sampler = property.Value;
                            break;
                        case PngInfoProperties.CfgScale:
                            settings.GuidanceScale = double.Parse(property.Value);
                            break;
                        case PngInfoProperties.Seed:
                            settings.Seed = long.Parse(property.Value);
                            break;
                        case PngInfoProperties.Size:
                            var size = property.Value.Split('x');

                            if (size.Length != 2)
                            {
                                break;
                            }

                            settings.Width = double.Parse(size[0]);
                            settings.Height = double.Parse(size[1]);
                            break;
                        case PngInfoProperties.DenoisingStrength:
                            settings.DenoisingStrength = double.Parse(property.Value);
                            break;
                        case PngInfoProperties.HiresUpscaler:
                            settings.Upscaler = property.Value;
                            settings.EnableUpscaling = !string.IsNullOrEmpty(property.Value);
                            break;
                        case PngInfoProperties.HiresUpscale:
                            settings.UpscaleLevel = int.Parse(property.Value);
                            break;
                        case PngInfoProperties.HiresSteps:
                            settings.UpscaleSteps = int.Parse(property.Value);
                            break;
                        case PngInfoProperties.Scheduler:
                        case PngInfoProperties.ScheduleType:
                            settings.Scheduler = property.Value.ToLower();
                            settings.ModelType = Enums.ModelType.ZImage;
                            break;
                        case PngInfoProperties.DistilledCfgScale:
                        case PngInfoProperties.DistilledCfgScaleKey:
                        case PngInfoProperties.Shift:
                            settings.DistilledCfgScale = double.Parse(property.Value);
                            break;
                        case PngInfoProperties.Model:
                            if (settings.Model == null)
                            {
                                var matchingModel = _models.FirstOrDefault(m => m.ModelName == property.Value);
                                if (matchingModel != null)
                                {
                                    settings.Model = (ModelViewModel)convertModelToViewModel(matchingModel);
                                }
                            }
                            break;
                        case PngInfoProperties.ModelHash:
                            if (settings.Model == null)
                            {
                                var matchingModel = _models.FirstOrDefault(m => m.Hash == property.Value);
                                if (matchingModel != null)
                                {
                                    settings.Model = (ModelViewModel)convertModelToViewModel(matchingModel);
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
                catch
                {
                    // Skip to the next property
                }
            }

            if (settings.Model == null)
            {
                settings.Model = (ModelViewModel)await GetSelectedModelAsync();
            }

            return settings;
        }

        public async Task RefreshResourcesAsync()
        {
            if (_client == null) return;

            await Task.WhenAll(
                Task.Run(async () => _samplers = await _client.Sdapi.V1.Samplers.GetAsync()),
                Task.Run(async () => _schedulers = await _client.Sdapi.V1.Schedulers.GetAsync()),
                Task.Run(async () => _promptStyles = await _client.Sdapi.V1.PromptStyles.GetAsync()),
                Task.Run(async () => _models = await _client.Sdapi.V1.SdModels.GetAsync()),
                Task.Run(async () => 
                {
                    var lorasNode = await _client.Sdapi.V1.Loras.GetAsync();
                    _loras = ParseLoras(lorasNode);
                }),
                Task.Run(async () => _upscalers = await _client.Sdapi.V1.Upscalers.GetAsync()),
                Task.Run(async () => _options = await _client.Sdapi.V1.OptionsPath.GetAsync())
            );
        }

        private List<LoraItem> ParseLoras(UntypedNode node)
        {
            var loras = new List<LoraItem>();
            if (node is UntypedArray array)
            {
                foreach (var item in array.GetValue())
                {
                    if (item is UntypedObject obj)
                    {
                        var properties = obj.GetValue();
                        var lora = new LoraItem();
                        
                        if (properties.TryGetValue("name", out var nameNode) && nameNode is UntypedString nameStr)
                        {
                            lora.Name = nameStr.GetValue();
                        }
                        
                        if (properties.TryGetValue("alias", out var aliasNode) && aliasNode is UntypedString aliasStr)
                        {
                            lora.Alias = aliasStr.GetValue();
                        }
                        
                        loras.Add(lora);
                    }
                }
            }
            return loras;
        }

        public Task<Dictionary<string, string>> GetSamplersAsync()
        {
            var result = new Dictionary<string, string>();

            if (_samplers == null)
            {
                return Task.FromResult(result);
            }

            foreach (var sampler in _samplers)
            {
                result.TryAdd(sampler.Name, sampler.Aliases?.FirstOrDefault() ?? sampler.Name);
            }

            return Task.FromResult(result);
        }

        public Task<List<string>> GetSchedulersAsync()
        {
            var result = new List<string>();

            if (_schedulers == null)
            {
                return Task.FromResult(result);
            }

            foreach (var scheduler in _schedulers)
            {
                result.Add(scheduler.Name);
            }

            return Task.FromResult(result);
        }

        public Task<List<IPromptStyleViewModel>> GetPromptStylesAsync()
        {
            var result = new List<IPromptStyleViewModel>();

            if (_promptStyles == null)
            {
                return Task.FromResult(result);
            }

            foreach (var item in _promptStyles)
            {
                result.Add(new PromptStyleViewModel
                {
                    Name = item.Name,
                    Prompt = item.Prompt,
                    NegativePrompt = item.NegativePrompt
                });
            }

            return Task.FromResult(result);
        }

        public Task<List<IModelViewModel>> GetModelsAsync()
        {
            var result = new List<IModelViewModel>();

            if (_models == null)
            {
                return Task.FromResult(result);
            }

            foreach (var model in _models)
            {
                var viewModel = convertModelToViewModel(model);
                if (viewModel != null)
                    result.Add(viewModel);
            }

            return Task.FromResult(result);
        }

        public Task<List<ILoraViewModel>> GetLorasAsync()
        {
            var result = new List<ILoraViewModel>();

            if (_loras == null)
            {
                return Task.FromResult(result);
            }

            foreach (var lora in _loras)
            {
                result.Add(new LoraViewModel
                {
                    Alias = lora.Alias,
                    Name = lora.Name
                });
            }

            return Task.FromResult(result);
        }

        public Task<List<IUpscalerViewModel>> GetUpscalersAsync()
        {
            var result = new List<IUpscalerViewModel>();

            if (_upscalers == null)
            {
                return Task.FromResult(result);
            }

            foreach (var upscaler in _upscalers)
            {
                result.Add(new UpscalerViewModel
                {
                    Name = upscaler.Name,
                    ModelName = upscaler.ModelName,
                    Scale = upscaler.Scale ?? 1.0,
                });
            }

            return Task.FromResult(result);
        }

        public Task<IModelViewModel> GetSelectedModelAsync()
        {
            var result = _serviceProvider.GetService<IModelViewModel>();

            if (_options == null || _models == null) return Task.FromResult(result);

            var checkpointHash = GetOptionValue(_options.SdCheckpointHash);
            var selectedModel = _models.FirstOrDefault(m => m.Sha256 == checkpointHash);

            if (selectedModel != null)
            {
                result.DisplayName = selectedModel.ModelName;
                result.Key = selectedModel.Title;
            }

            return Task.FromResult(result);
        }

        public async Task SaveSettingsAsync(PromptSettings settings)
        {
            if (_client == null) return;

            _options = await _client.Sdapi.V1.OptionsPath.GetAsync();

            var requestBody = new OptionsPostRequestBody();

            if (settings.Model != null)
            {
                var selectedModel = _models?.FirstOrDefault(m => m.Title == settings.Model.Key);
                var currentCheckpointHash = GetOptionValue(_options.SdCheckpointHash);

                if (selectedModel != null &&
                    currentCheckpointHash != selectedModel.Sha256)
                {
                    requestBody.AdditionalData.Add("sd_model_checkpoint", selectedModel.Title);
                }
            }
            
            if (!requestBody.AdditionalData.Any())
            {
                return;
            }

            await _client.Sdapi.V1.OptionsPath.PostAsync(requestBody);

            await RefreshResourcesAsync();
        }

        private IModelViewModel convertModelToViewModel(SDModelItem model)
        {
            if (model == null)
            {
                return null;
            }

            var viewModel = _serviceProvider.GetService<IModelViewModel>();

            viewModel.DisplayName = model.ModelName;
            viewModel.Key = model.Title;

            return viewModel;
        }

        private string GetOptionValue(UntypedNode node)
        {
            if (node is UntypedString str)
            {
                return str.GetValue();
            }
            return null;
        }
    }
}
