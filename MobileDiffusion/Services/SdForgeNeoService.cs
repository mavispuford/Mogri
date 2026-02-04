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
using Microsoft.Kiota.Abstractions.Serialization;
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
        private SdForgeNeoClient? _client;
        private SdForgeNeoClient? _progressClient;
        private string? _baseUrl;
        private CancellationTokenSource? _mainRequestCancellationSource;
        private Task? _initializeTask;

        private List<SamplerItem>? _samplers;
        private List<SchedulerItem>? _schedulers;
        private List<PromptStyleItem>? _promptStyles;
        private List<SDModelItem>? _models;
        private List<LoraItem>? _loras;
        private List<UpscalerItem>? _upscalers;
        private Options? _options;

        public bool Initialized { get; private set; }

        public SdForgeNeoService(IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider)
        {
            _httpClientFactory = httpClientFactory;
            _serviceProvider = serviceProvider;
        }

        public async Task InitializeAsync()
        {
            _baseUrl = Preferences.Default.Get(Constants.PreferenceKeys.ServerUrl, string.Empty);

            if (string.IsNullOrWhiteSpace(_baseUrl) || !_baseUrl.Contains("http"))
            {
                Initialized = false;
                return;
            }

            Uri baseUri;
            try
            {
                baseUri = new Uri(_baseUrl);
            }
            catch
            {
                Initialized = false;
                return;
            }

            // Initialize client
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = baseUri;
            httpClient.Timeout = TimeSpan.FromMinutes(15);

            // Kiota setup
            var authProvider = new AnonymousAuthenticationProvider();
            var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);
            adapter.BaseUrl = _baseUrl;
            _client = new SdForgeNeoClient(adapter);

            // Initialize progress client
            var progressHttpClient = _httpClientFactory.CreateClient();
            progressHttpClient.BaseAddress = baseUri;
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
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _client.Sdapi.V1.OptionsPath.GetAsync(cancellationToken: cts.Token);
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

            TextToImageResponse? txt2ImgResponse = null;

            var textToImageTask = Task.Run(async () =>
            {
                if (_client == null) return;
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

            ApiResponse? apiResponse = null;
            var skipCurrentImage = false;
            var finished = false;

            while (!finished)
            {
                try
                {
                    apiResponse = await getCurrentProgress(progressToken, skipCurrentImage);

                    if (apiResponse.ResponseObject is ProgressResponse pr && pr.IsInterrupted)
                    {
                        try
                        {
                            await textToImageTask;
                        }
                        catch
                        {
                            // Ignore
                        }

                        var generationResponse = new GenerationResponse
                        {
                            Images = txt2ImgResponse?.Images ?? [],
                            Info = txt2ImgResponse?.Info ?? string.Empty
                        };

                        apiResponse = new ApiResponse
                        {
                            ResponseObject = generationResponse,
                            Progress = 1f
                        };

                        finished = true;
                    }
                }
                catch (OperationCanceledException)
                {
                    await textToImageTask;

                    // Set the final response
                    var generationResponse = new GenerationResponse
                    {
                        Images = txt2ImgResponse?.Images ?? [],
                        Info = txt2ImgResponse?.Info ?? string.Empty
                    };

                    apiResponse = new ApiResponse
                    {
                        ResponseObject = generationResponse,
                        Progress = 1f
                    };

                    finished = true;
                }

                if (apiResponse != null)
                {
                    yield return apiResponse;
                }

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

            ImageToImageResponse? img2ImgResponse = null;

            var imgToImageTask = Task.Run(async () =>
            {
                if (_client == null) return;
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

            ApiResponse? apiResponse = null;
            var skipCurrentImage = true;
            var finished = false;

            while (!finished)
            {
                try
                {
                    apiResponse = await getCurrentProgress(progressToken, skipCurrentImage);

                    if (apiResponse.ResponseObject is ProgressResponse pr && pr.IsInterrupted)
                    {
                        try
                        {
                            await imgToImageTask;
                        }
                        catch
                        {
                            // Ignore
                        }

                        var generationResponse = new GenerationResponse
                        {
                            Images = img2ImgResponse?.Images ?? [],
                            Info = img2ImgResponse?.Info ?? string.Empty
                        };

                        apiResponse = new ApiResponse
                        {
                            ResponseObject = generationResponse,
                            Progress = 1f
                        };

                        finished = true;
                    }
                }
                catch (OperationCanceledException)
                {
                    await imgToImageTask;

                    // Set the final response
                    var generationResponse = new GenerationResponse
                    {
                        Images = img2ImgResponse?.Images ?? [],
                        Info = img2ImgResponse?.Info ?? string.Empty
                    };

                    apiResponse = new ApiResponse
                    {
                        ResponseObject = generationResponse,
                        Progress = 1f
                    };

                    finished = true;
                }

                if (apiResponse != null)
                {
                    yield return apiResponse;
                }

                // Skip current image every other time
                // skipCurrentImage = !skipCurrentImage;
            }
        }

        private async Task<ApiResponse> getCurrentProgress(CancellationToken token, bool skipCurrentImage)
        {
            if (_progressClient == null)
            {
                throw new InvalidOperationException("API Client not initialized");
            }

            return await Task.Run(async () =>
            {
                await Task.Delay(500, token);

                token.ThrowIfCancellationRequested();

                // Note: Kiota client doesn't support query parameters easily in the generated method signature if not exposed.
                // The generated method is: GetAsync(Action<RequestConfiguration<DefaultQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
                // We need to set skip_current_image query param.

                var progressGetResponse = await _progressClient.Sdapi.V1.Progress.GetAsync((config) =>
                {
                    config.QueryParameters.SkipCurrentImage = skipCurrentImage;
                }, token);

                if (progressGetResponse == null)
                {
                    return new ApiResponse { Progress = 0, ResponseObject = new ProgressResponse() };
                }

                var progress = progressGetResponse.EtaRelative > 0 ? progressGetResponse.Progress : 1d;

                var isInterrupted = false;
                if (progressGetResponse.State?.AdditionalData != null &&
                    progressGetResponse.State.AdditionalData.TryGetValue("interrupted", out var interruptedObj))
                {
                    if (interruptedObj is UntypedBoolean interruptedBool)
                    {
                        isInterrupted = interruptedBool.GetValue();
                    }
                    else if (interruptedObj is bool b)
                    {
                        isInterrupted = b;
                    }
                }

                var progressResponse = new ProgressResponse
                {
                    Progress = progressGetResponse.Progress ?? 0,
                    EtaRelative = progressGetResponse.EtaRelative ?? 0,
                    CurrentImage = progressGetResponse.CurrentImage,
                    IsInterrupted = isInterrupted
                };

                var progressApiResponse = new ApiResponse
                {
                    ResponseObject = progressResponse,
                    Progress = progress ?? 0
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
            // However, if the user has explicitly set a mask blur, we should honor it.
            request.MaskBlur = settings.MaskBlur;
            request.MaskBlurX = settings.MaskBlur;
            request.MaskBlurY = settings.MaskBlur;
            request.MaskRound = false;

            if (settings.ModelType == Enums.ModelType.ZImage)
            {
                request.Scheduler = settings.Scheduler;

                if (settings.DistilledCfgScale.HasValue)
                {
                    request.AdditionalData.Add("distilled_cfg_scale", settings.DistilledCfgScale.Value);
                }
            }

            foreach (var lora in settings.Loras)
            {
                request.Prompt += $" <lora:{lora.Name}:{lora.Strength:F1}>";
            }

            return request;
        }

        public Task<byte[]> GetImageBytesAsync(string url) => throw new NotImplementedException();
        public async Task<PromptSettings?> GetImageInfoAsync(string base64EncodedImage)
        {
            if (_client == null || string.IsNullOrWhiteSpace(base64EncodedImage)) return null;

            var request = new PNGInfoRequest()
            {
                Image = base64EncodedImage
            };

            var imageInfoResult = await _client.Sdapi.V1.PngInfo.PostAsync(request);

            if (imageInfoResult?.Info == null || string.IsNullOrEmpty(imageInfoResult.Info))
            {
                return null;
            }

            var newLineSplit = imageInfoResult.Info.Split('\n');

            if (newLineSplit.Length <= 1)
            {
                return null;
            }

            var commaSplit = newLineSplit.LastOrDefault()?.Split(',') ?? [];

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

                if (float.TryParse(match.Groups[2].Value, out var strength))
                {
                    var lora = new LoraViewModel
                    {
                        Name = name,
                        Strength = strength,
                    };

                    loras.Add(lora);
                }

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
                            if (int.TryParse(property.Value, out var steps)) settings.Steps = steps;
                            break;
                        case PngInfoProperties.Sampler:
                            settings.Sampler = property.Value;
                            break;
                        case PngInfoProperties.CfgScale:
                            if (double.TryParse(property.Value, out var cfg)) settings.GuidanceScale = cfg;
                            break;
                        case PngInfoProperties.Seed:
                            if (long.TryParse(property.Value, out var seed)) settings.Seed = seed;
                            break;
                        case PngInfoProperties.Size:
                            var size = property.Value.Split('x');

                            if (size.Length != 2)
                            {
                                break;
                            }

                            if (double.TryParse(size[0], out var width)) settings.Width = width;
                            if (double.TryParse(size[1], out var height)) settings.Height = height;
                            break;
                        case PngInfoProperties.DenoisingStrength:
                            if (double.TryParse(property.Value, out var denoise)) settings.DenoisingStrength = denoise;
                            break;
                        case PngInfoProperties.HiresUpscaler:
                            settings.Upscaler = property.Value;
                            settings.EnableUpscaling = !string.IsNullOrEmpty(property.Value);
                            break;
                        case PngInfoProperties.HiresUpscale:
                            if (int.TryParse(property.Value, out var upscale)) settings.UpscaleLevel = upscale;
                            break;
                        case PngInfoProperties.HiresSteps:
                            if (int.TryParse(property.Value, out var hrSteps)) settings.UpscaleSteps = hrSteps;
                            break;
                        case PngInfoProperties.Scheduler:
                        case PngInfoProperties.ScheduleType:
                            settings.Scheduler = property.Value.ToLower();
                            settings.ModelType = Enums.ModelType.ZImage;
                            break;
                        case PngInfoProperties.DistilledCfgScale:
                        case PngInfoProperties.DistilledCfgScaleKey:
                        case PngInfoProperties.Shift:
                            if (double.TryParse(property.Value, out var distCfg)) settings.DistilledCfgScale = distCfg;
                            break;
                        case PngInfoProperties.Model:
                            if (settings.Model == null && _models != null)
                            {
                                var matchingModel = _models.FirstOrDefault(m => m.ModelName == property.Value);
                                if (matchingModel != null)
                                {
                                    settings.Model = (ModelViewModel?)convertModelToViewModel(matchingModel);
                                }
                            }
                            break;
                        case PngInfoProperties.ModelHash:
                            if (settings.Model == null && _models != null)
                            {
                                var matchingModel = _models.FirstOrDefault(m => m.Hash == property.Value);
                                if (matchingModel != null)
                                {
                                    settings.Model = (ModelViewModel?)convertModelToViewModel(matchingModel);
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
                settings.Model = (ModelViewModel?)await GetSelectedModelAsync();
            }

            return settings;
        }

        public async Task RefreshResourcesAsync()
        {
            if (_client == null || _baseUrl == null) return;

            // 1. Connectivity Probe
            // We use a dedicated, short-lived HttpClient to perform a strict connectivity check.
            // This bypasses any potential middleware/retries in the main client pipeline and enforces a hard timeout.
            using (var probeClient = _httpClientFactory.CreateClient())
            {
                probeClient.BaseAddress = new Uri(_baseUrl);
                probeClient.Timeout = TimeSpan.FromSeconds(4); // Hard cap on the probe
                try
                {
                    using var probeCts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                    // Just fetch headers to be fast
                    using var response = await probeClient.GetAsync("sdapi/v1/options", HttpCompletionOption.ResponseHeadersRead, probeCts.Token);
                    response.EnsureSuccessStatusCode();
                }
                catch
                {
                    // If the probe fails (timeout, connection refused, 404, etc.), abort immediately.
                    throw;
                }
            }

            // 2. If connected, proceed with parallel resource fetching.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var tasks = new List<Task>
            {
                // Options is strictly required and fetched first/parallel
                Task.Run(async () => _options = await _client.Sdapi.V1.OptionsPath.GetAsync(cancellationToken: cts.Token)),
                Task.Run(async () => _samplers = await _client.Sdapi.V1.Samplers.GetAsync(cancellationToken: cts.Token)),
                Task.Run(async () => _schedulers = await _client.Sdapi.V1.Schedulers.GetAsync(cancellationToken: cts.Token)),
                Task.Run(async () => _promptStyles = await _client.Sdapi.V1.PromptStyles.GetAsync(cancellationToken: cts.Token)),
                Task.Run(async () => _models = await _client.Sdapi.V1.SdModels.GetAsync(cancellationToken: cts.Token)),
                Task.Run(async () =>
                {
                    var lorasNode = await _client.Sdapi.V1.Loras.GetAsync(cancellationToken: cts.Token);
                    _loras = ParseLoras(lorasNode);
                }),
                Task.Run(async () => _upscalers = await _client.Sdapi.V1.Upscalers.GetAsync(cancellationToken: cts.Token))
            };

            try
            {
                var remainingTasks = tasks.ToList();
                while (remainingTasks.Count > 0)
                {
                    var finishedTask = await Task.WhenAny(remainingTasks);

                    if (finishedTask.IsFaulted || finishedTask.IsCanceled)
                    {
                        cts.Cancel();
                        await finishedTask;
                    }

                    remainingTasks.Remove(finishedTask);
                }
            }
            catch
            {
                cts.Cancel();
                throw;
            }
        }

        private List<LoraItem> ParseLoras(UntypedNode? node)
        {
            var loras = new List<LoraItem>();
            if (node is UntypedArray array)
            {
                var items = array.GetValue();
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (item is UntypedObject obj)
                        {
                            var properties = obj.GetValue();
                            if (properties == null) continue;

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
                var name = sampler.Name;
                if (!string.IsNullOrEmpty(name))
                {
                    result.TryAdd(name, sampler.Aliases?.FirstOrDefault() ?? name);
                }
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
                if (!string.IsNullOrEmpty(scheduler.Name))
                {
                    result.Add(scheduler.Name);
                }
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
                    Name = item.Name ?? string.Empty,
                    Prompt = item.Prompt ?? string.Empty,
                    NegativePrompt = item.NegativePrompt ?? string.Empty
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
                // convertModelToViewModel now returns IModelViewModel? so we can filter nulls
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
                    Alias = lora.Alias ?? string.Empty,
                    Name = lora.Name ?? string.Empty
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
                    Name = upscaler.Name ?? string.Empty,
                    ModelName = upscaler.ModelName ?? string.Empty,
                    Scale = upscaler.Scale ?? 1.0,
                });
            }

            return Task.FromResult(result);
        }

        public Task<IModelViewModel?> GetSelectedModelAsync()
        {
            var result = _serviceProvider.GetService<IModelViewModel>();

            if (_options == null || _models == null) return Task.FromResult<IModelViewModel?>(result);

            var checkpointHash = GetOptionValue(_options.SdCheckpointHash);
            var selectedModel = _models.FirstOrDefault(m => m.Sha256 == checkpointHash);

            if (selectedModel != null && result != null)
            {
                result.DisplayName = selectedModel.ModelName ?? string.Empty;
                result.Key = selectedModel.Title ?? string.Empty;
            }

            return Task.FromResult<IModelViewModel?>(result);
        }

        public async Task SaveSettingsAsync(PromptSettings settings)
        {
            if (_client == null || settings == null) return;

            _options = await _client.Sdapi.V1.OptionsPath.GetAsync();
            if (_options == null) return;

            var requestBody = new OptionsPostRequestBody();

            if (settings.Model != null)
            {
                var selectedModel = _models?.FirstOrDefault(m => m.Title == settings.Model.Key);
                var currentCheckpointHash = GetOptionValue(_options.SdCheckpointHash);

                if (selectedModel != null)
                {
                    if (currentCheckpointHash != selectedModel.Sha256)
                    {
                        // TryAdd to avoid exception if key exists (though new request body shouldn't have)
                        requestBody.AdditionalData.TryAdd("sd_model_checkpoint", selectedModel.Title);
                    }

                    // Handle Z-Image Turbo setting
                    string desiredUnetStorage = "Automatic";
                    if (settings.ModelType == MobileDiffusion.Enums.ModelType.ZImage && settings.Loras?.Any() == true)
                    {
                        desiredUnetStorage = "Automatic (fp16 LoRA)";
                    }

                    string? currentUnetStorage = null;
                    if (_options.AdditionalData.TryGetValue("forge_unet_storage_dtype", out var unetStorageObj))
                    {
                        if (unetStorageObj is UntypedString unetStorageStr)
                        {
                            currentUnetStorage = unetStorageStr.GetValue();
                        }
                        else if (unetStorageObj is string str)
                        {
                            currentUnetStorage = str;
                        }
                    }

                    if (currentUnetStorage != desiredUnetStorage)
                    {
                        requestBody.AdditionalData.TryAdd("forge_unet_storage_dtype", desiredUnetStorage);
                    }
                }
            }

            if (!requestBody.AdditionalData.Any())
            {
                return;
            }

            await _client.Sdapi.V1.OptionsPath.PostAsync(requestBody);

            await RefreshResourcesAsync();
        }

        public async Task<bool> CancelAsync()
        {
            if (_client == null) return false;
            try
            {
                await _client.Sdapi.V1.Interrupt.PostAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private IModelViewModel? convertModelToViewModel(SDModelItem model)
        {
            if (model == null)
            {
                return null;
            }

            var viewModel = _serviceProvider.GetService<IModelViewModel>();
            if (viewModel == null) return null;

            viewModel.DisplayName = model.ModelName ?? string.Empty;
            viewModel.Key = model.Title ?? string.Empty;

            return viewModel;
        }

        private string? GetOptionValue(UntypedNode? node)
        {
            if (node is UntypedString str)
            {
                return str.GetValue();
            }
            return null;
        }
    }
}
