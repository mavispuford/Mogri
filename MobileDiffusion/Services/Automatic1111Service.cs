using MobileDiffusion.Clients.Automatic1111;
using MobileDiffusion.Helpers;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using MobileDiffusion.Models.Automatic1111;
using MobileDiffusion.ViewModels;
using Newtonsoft.Json;
using System.Dynamic;
using System.Text.RegularExpressions;

namespace MobileDiffusion.Services
{
    internal class Automatic1111Service : IImageGenerationService
    {
        private static class PngInfoProperties
        {
            public const string Steps = nameof(Steps);
            public const string Sampler = nameof(Sampler);
            public const string CfgScale = "CFG Scale";
            public const string Seed = nameof(Seed);
            public const string Size = nameof(Size);
            public const string ModelHash = "Model hash";
            public const string Model = nameof(Model);
            public const string LoraHashes = "Lora hashes";
            public const string DenoisingStrength = "Denoising strength";
            public const string Eta = nameof(Eta);
            public const string Version = nameof(Version);
            public const string HiresUpscaler = "Hires upscaler";
            public const string HiresUpscale = "Hires upscale";
            public const string HiresSteps = "Hires steps";
        }

        private Regex _loraRegex = new Regex("<lora:([^:]*):([^>]*)>", RegexOptions.Compiled);

        /// <summary>
        ///     Some operations take several minutes to complete, especially with older hardware.
        /// </summary>
        private const int RequestTimeoutMinutes = 15;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceProvider _serviceProvider;

        private ICollection<SamplerItem> _samplers;
        private ICollection<PromptStyleItem> _promptStyles;
        private ICollection<SDModelItem> _models;
        private ICollection<LoraItem> _loras;
        private ICollection<UpscalerItem> _upscalers;
        private Options _options;

        private Task _initializeTask;
        private CancellationTokenSource _mainRequestCancellationSource;

        public bool Initialized { get; private set; }

        public Automatic1111Service(IHttpClientFactory httpClientFactory,
            IServiceProvider serviceProvider)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task InitializeAsync()
        {
            var baseUrl = Preferences.Default.Get(Constants.PreferenceKeys.ServerUrl, string.Empty);

            if (string.IsNullOrEmpty(baseUrl) || !baseUrl.Contains("http"))
            {
                Initialized = false;
                return;
            }

            if (_initializeTask == null || _initializeTask.Status != TaskStatus.Running)
            {
                _initializeTask = await Task.Run(async () =>
                {
                    // For some reason in .NET 8 (not .NET 7), if we don't manually get an HttpClient and make any GET call before the Automatic1111 client uses it,
                    // We'll get the following exception: {System.ObjectDisposedException: Cannot access a disposed object. Object name: 'AndroidMessageHandler'.
                    var httpClient = getHttpClient(TimeSpan.FromSeconds(5));
                    var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, baseUrl)).ConfigureAwait(false);
                }).ContinueWith(async task =>
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
            var httpClient = getHttpClient(TimeSpan.FromSeconds(5));
            var auto1111Client = getAuto1111Client(httpClient);

            try
            {
                await auto1111Client._lambda__internal_ping_getAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public Task<byte[]> GetImageBytesAsync(string url)
        {
            throw new NotImplementedException();
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

        public async Task<PromptSettings> GetImageInfoAsync(string base64EncodedImage)
        {
            var httpClient = getHttpClient(TimeSpan.FromSeconds(10));
            var auto1111Client = getAuto1111Client(httpClient);

            var request = new PNGInfoRequest()
            {
                Image = base64EncodedImage
            };

            var imageInfoResult = await auto1111Client.Pnginfoapi_sdapi_v1_png_info_postAsync(request);

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

            foreach (var match in loraMatches.Where(m => m.Success))
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
                    switch (property.Key)
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
                        case PngInfoProperties.Model:
                            if (settings.Model == null)
                            {
                                var matchingModel = _models.FirstOrDefault(m => m.Model_name == property.Value);
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
            var httpClient = getHttpClient(TimeSpan.FromSeconds(10));
            var auto1111Client = getAuto1111Client(httpClient);

            await Task.WhenAll(Task.Run(async () =>
            {
                _samplers = await auto1111Client.Get_samplers_sdapi_v1_samplers_getAsync();
            }),
            Task.Run(async () =>
            {
                _promptStyles = await auto1111Client.Get_prompt_styles_sdapi_v1_prompt_styles_getAsync();
            }),
            Task.Run(async () =>
            {
                var loras = await auto1111Client.Get_loras_sdapi_v1_loras_getAsync();

                var lorasString = JsonConvert.SerializeObject(loras);

                _loras = JsonConvert.DeserializeObject<ICollection<LoraItem>>(lorasString);
            }),
            Task.Run(async () =>
            {
                _models = await auto1111Client.Get_sd_models_sdapi_v1_sd_models_getAsync();
            }),
            Task.Run(async () =>
            {
                _upscalers = await auto1111Client.Get_upscalers_sdapi_v1_upscalers_getAsync();
            }),
            Task.Run(async () =>
            {
                _options = await auto1111Client.Get_config_sdapi_v1_options_getAsync();
            }));
        }

        private HttpClient getHttpClient(TimeSpan? timeout = null)
        {
            var httpClient = _httpClientFactory.CreateClient(Microsoft.Extensions.Options.Options.DefaultName);

            if (timeout != null)
            {
                httpClient.Timeout = timeout.Value;
            }

            return httpClient;
        }

        private Client getAuto1111Client(HttpClient httpClient)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            var baseUrl = Preferences.Default.Get(Constants.PreferenceKeys.ServerUrl, string.Empty);

            var automaticClient = new Client(baseUrl, httpClient);

            return automaticClient;
        }

        private static StableDiffusionProcessingTxt2Img txt2ImageRequestFromSettings(PromptSettings settings)
        {
            var request = new StableDiffusionProcessingTxt2Img();

            request.N_iter = settings.BatchCount;
            request.Batch_size = settings.BatchSize;
            request.Cfg_scale = settings.GuidanceScale;
            request.Restore_faces = settings.EnableGfpgan;
            request.Width = (int)settings.Width;
            request.Height = (int)settings.Height;
            request.Denoising_strength = settings.DenoisingStrength;
            request.Steps = settings.Steps;
            request.Seed = settings.Seed;
            request.Tiling = settings.Seamless == Enums.OnOff.on;

            // Hires Fix
            request.Enable_hr = settings.EnableUpscaling &&
                !string.IsNullOrEmpty(settings.Upscaler) &&
                settings.UpscaleLevel > 0 &&
                settings.UpscaleSteps > 0;
            request.Hr_scale = settings.UpscaleLevel;
            request.Hr_second_pass_steps = settings.UpscaleSteps;
            request.Hr_upscaler = settings.Upscaler;

            var combinedPromptAndStyles = settings.GetCombinedPromptAndPromptStyles();
            request.Prompt = combinedPromptAndStyles.Prompt;
            request.Negative_prompt = combinedPromptAndStyles.NegativePrompt;

            request.Sampler_name = settings.Sampler;

            foreach (var lora in settings.Loras)
            {
                request.Prompt += $" <lora:{lora.Name}:{lora.Strength:F1}>";
            }

            return request;
        }

        private static StableDiffusionProcessingImg2Img image2ImageRequestFromSettings(PromptSettings settings)
        {
            var request = new StableDiffusionProcessingImg2Img();

            request.N_iter = settings.BatchCount;
            request.Batch_size = settings.BatchSize;
            request.Cfg_scale = settings.GuidanceScale;
            request.Restore_faces = settings.EnableGfpgan;
            request.Width = (int)settings.Width;
            request.Height = (int)settings.Height;
            request.Denoising_strength = settings.DenoisingStrength;
            request.Steps = settings.Steps;
            request.Seed = (int)settings.Seed;
            request.Tiling = settings.Seamless == Enums.OnOff.on;

            var combinedPromptAndStyles = settings.GetCombinedPromptAndPromptStyles();
            request.Prompt = combinedPromptAndStyles.Prompt;
            request.Negative_prompt = combinedPromptAndStyles.NegativePrompt;

            request.Sampler_name = settings.Sampler;
            request.Init_images = new List<object>
            {
                settings.InitImage
            };
            request.Mask = !string.IsNullOrEmpty(settings.Mask) ? settings.Mask : null; // Make sure mask is null if it's an empty string
            request.Inpainting_fill = 1;

            // Because we colorize the image, blurring the mask would cause some of the colorized pixels to stay
            request.Mask_blur = 0;
            request.Mask_blur_x = 0;
            request.Mask_blur_y = 0;
            request.Mask_round = false;

            foreach (var lora in settings.Loras)
            {
                request.Prompt += $" <lora:{lora.Name}:{lora.Strength:F1}>";
            }

            return request;
        }

        private async IAsyncEnumerable<ApiResponse> sendTextToImageRequest(PromptSettings settings)
        {
            var httpClient = getHttpClient(TimeSpan.FromMinutes(RequestTimeoutMinutes));
            var auto1111Client = getAuto1111Client(httpClient);

            var request = txt2ImageRequestFromSettings(settings);

            if (!_mainRequestCancellationSource?.IsCancellationRequested ?? false)
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
                    txt2ImgResponse = await auto1111Client.Text2imgapi_sdapi_v1_txt2img_postAsync(request, _mainRequestCancellationSource?.Token ?? CancellationToken.None);
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
                    apiResponse = await getCurrentProgress(auto1111Client, progressToken, skipCurrentImage);
                }
                catch (OperationCanceledException)
                {
                    // Set the final response
                    apiResponse = new ApiResponse
                    {
                        StableDiffusionApi = Enums.StableDiffusionApi.Automatic1111,
                        ResponseObject = txt2ImgResponse,
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
            var httpClient = getHttpClient(TimeSpan.FromMinutes(RequestTimeoutMinutes));
            var auto1111Client = getAuto1111Client(httpClient);

            var request = image2ImageRequestFromSettings(settings);

            if (_mainRequestCancellationSource?.IsCancellationRequested ?? false)
            {
                _mainRequestCancellationSource.Cancel();
            }

            _mainRequestCancellationSource = new CancellationTokenSource();
            var progressCancellationTokenSource = new CancellationTokenSource();
            var progressToken = progressCancellationTokenSource.Token;

            ImageToImageResponse img2ImgResponse = null;

            var textToImageTask = Task.Run(async () =>
            {
                try
                {
                    img2ImgResponse = await auto1111Client.Img2imgapi_sdapi_v1_img2img_postAsync(request, _mainRequestCancellationSource?.Token ?? CancellationToken.None);
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
                    apiResponse = await getCurrentProgress(auto1111Client, progressToken, skipCurrentImage);
                }
                catch (OperationCanceledException)
                {
                    // Set the final response
                    apiResponse = new ApiResponse
                    {
                        StableDiffusionApi = Enums.StableDiffusionApi.Automatic1111,
                        ResponseObject = img2ImgResponse,
                        Progress = 1f
                    };

                    finished = true;
                }

                yield return apiResponse;

                // Skip current image every other time
                skipCurrentImage = !skipCurrentImage;
            }
        }

        private async Task<ApiResponse> getCurrentProgress(Client client, CancellationToken token, bool skipCurrentImage)
        {
            return await Task.Run(async () =>
            {
                await Task.Delay(500, token);

                token.ThrowIfCancellationRequested();

                var progressGetResponse = await client.Progressapi_sdapi_v1_progress_getAsync(true, token);
                //var progressGetResponse = await client.Progressapi_sdapi_v1_progress_getAsync(skipCurrentImage, token);

                var progress = progressGetResponse.Eta_relative > 0 ? progressGetResponse.Progress : 1d;

                var progressApiResponse = new ApiResponse
                {
                    StableDiffusionApi = Enums.StableDiffusionApi.Automatic1111,
                    ResponseObject = progressGetResponse,
                    Progress = progress
                };

                return progressApiResponse;
            });
        }

        private async Task retrieveOptionsAsync()
        {
            var httpClient = getHttpClient(TimeSpan.FromMinutes(RequestTimeoutMinutes));
            var auto1111Client = getAuto1111Client(httpClient);

            _options = await auto1111Client.Get_config_sdapi_v1_options_getAsync();
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
                result.TryAdd(sampler.Name, sampler.Aliases.FirstOrDefault());
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
                    NegativePrompt = item.Negative_prompt
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

                result.Add(viewModel);
            }

            return Task.FromResult(result);
        }

        private IModelViewModel convertModelToViewModel(SDModelItem model)
        {
            if (model == null)
            {
                return null;
            }

            var viewModel = _serviceProvider.GetService<IModelViewModel>();

            viewModel.DisplayName = model.Model_name;
            viewModel.Key = model.Title;

            return viewModel;
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

        public Task<IModelViewModel> GetSelectedModelAsync()
        {
            var result = _serviceProvider.GetService<IModelViewModel>();

            var selectedModel = _models.FirstOrDefault(m => m.Title == _options.Sd_model_checkpoint);

            if (selectedModel != null)
            {
                result.DisplayName = selectedModel.Model_name;
                result.Key = selectedModel.Title;
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
                    ModelName = upscaler.Model_name,
                    Scale = upscaler.Scale,
                });
            }

            return Task.FromResult(result);
        }

        public async Task SaveSettingsAsync(PromptSettings settings)
        {
            var httpClient = getHttpClient(TimeSpan.FromMinutes(RequestTimeoutMinutes));
            var auto1111Client = getAuto1111Client(httpClient);

            if (_options == null)
            {
                _options = await auto1111Client.Get_config_sdapi_v1_options_getAsync();
            }

            var request = new ExpandoObject();

            if (settings.Model != null)
            {
                var selectedModel = _models.FirstOrDefault(m => m.Title == settings.Model.Key);

                if (selectedModel != null ||
                    _options.Sd_model_checkpoint != selectedModel.Title)
                {
                    request.TryAdd(nameof(_options.Sd_model_checkpoint).ToLower(), selectedModel.Title);
                }
            }

            var requestObjects = (IDictionary<string, object>)request;
            
            if (!requestObjects.Any())
            {
                return;
            }

            var response = await auto1111Client.Set_config_sdapi_v1_options_postAsync(request);

            await RefreshResourcesAsync();
        }
    }
}
