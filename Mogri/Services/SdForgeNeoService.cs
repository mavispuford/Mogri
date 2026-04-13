using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;
using Mogri.Enums;
using Mogri.Clients.SdForgeNeo;
using Mogri.Clients.SdForgeNeo.Models;
using Mogri.Clients.SdForgeNeo.Sdapi.V1.Options;
using Mogri.ViewModels;
using Mogri.Helpers;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Abstractions.Serialization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace Mogri.Services
{
    /// <summary>
    /// Implementation of IImageGenerationBackend for the "SD Forge Neo" (Automatic1111 fork) API.
    /// Handles communication with the /sdapi/v1 endpoints.
    /// </summary>
    public class SdForgeNeoService : IImageGenerationBackend
    {
        public string Name => "SD Forge Neo";
        public BackendCapabilities Capabilities => BackendCapabilities.Full;
        
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceProvider _serviceProvider;
        private SdForgeNeoClient? _client;
        private SdForgeNeoClient? _progressClient;
        private string? _baseUrl;
        private CancellationTokenSource? _mainRequestCancellationSource;
        private Task? _initializeTask;

        private List<SamplerItem>? _samplers;
        private List<SchedulerItem>? _schedulers;
        private List<SDModelItem>? _models;
        private List<LoraItem>? _loras;
        private List<UpscalerItem>? _upscalers;
        private List<string>? _moduleVaes;
        private List<string>? _textEncoders;
        private Options? _options;

        public bool Initialized { get; private set; }

        public SdForgeNeoService(IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider)
        {
            _httpClientFactory = httpClientFactory;
            _serviceProvider = serviceProvider;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            Initialized = false;

            _baseUrl = Preferences.Default.Get(Constants.PreferenceKeys.ServerUrl, string.Empty);

            if (string.IsNullOrWhiteSpace(_baseUrl) || !_baseUrl.Contains("http"))
            {
                return;
            }

            Uri baseUri;
            try
            {
                baseUri = new Uri(_baseUrl);
            }
            catch
            {
                return;
            }

            // Initialize client
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = baseUri;
            httpClient.Timeout = TimeSpan.FromMinutes(15);

            var customAuthName = Preferences.Default.Get(Constants.PreferenceKeys.AuthHeaderName, string.Empty);
            var customAuthValue = Preferences.Default.Get(Constants.PreferenceKeys.AuthHeaderValue, string.Empty);

            if (!string.IsNullOrWhiteSpace(customAuthName) && !string.IsNullOrWhiteSpace(customAuthValue))
            {
                httpClient.DefaultRequestHeaders.Add(customAuthName, customAuthValue);
            }

            // Kiota setup
            var authProvider = new AnonymousAuthenticationProvider();
            var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);
            adapter.BaseUrl = _baseUrl;
            _client = new SdForgeNeoClient(adapter);

            // Initialize progress client
            var progressHttpClient = _httpClientFactory.CreateClient();
            progressHttpClient.BaseAddress = baseUri;
            progressHttpClient.Timeout = TimeSpan.FromSeconds(10);

            if (!string.IsNullOrWhiteSpace(customAuthName) && !string.IsNullOrWhiteSpace(customAuthValue))
            {
                progressHttpClient.DefaultRequestHeaders.Add(customAuthName, customAuthValue);
            }

            var progressAdapter = new HttpClientRequestAdapter(authProvider, httpClient: progressHttpClient);
            progressAdapter.BaseUrl = _baseUrl;
            _progressClient = new SdForgeNeoClient(progressAdapter);

            if (_initializeTask == null || _initializeTask.Status != TaskStatus.Running)
            {
                _initializeTask = Task.Run(async () =>
                {
                    await RefreshResourcesAsync(cancellationToken);
                }, cancellationToken);
            }

            try 
            {
                await _initializeTask;
            }
            finally
            {
                _initializeTask = null;
            }

            Initialized = true;
        }

        public async Task<bool> CheckServerAsync(CancellationToken cancellationToken = default)
        {
            if (_client == null)
            {
                await InitializeAsync(cancellationToken);
                if (_client == null) return false;
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                
                await _client.Sdapi.V1.OptionsPath.GetAsync(cancellationToken: cts.Token);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async IAsyncEnumerable<ApiResponse> SubmitImageRequestAsync(PromptSettings settings, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (settings is null)
            {
                yield break;
            }

            if (string.IsNullOrEmpty(settings.InitImage))
            {
                // No image - Just do txt2img
                await foreach (ApiResponse apiResponse in sendTextToImageRequest(settings, cancellationToken))
                {
                    yield return apiResponse;
                }
            }
            else
            {
                // Image included - Do img2img
                await foreach (ApiResponse apiResponse in sendImageToImageRequest(settings, cancellationToken))
                {
                    yield return apiResponse;
                }
            }
        }

        private async IAsyncEnumerable<ApiResponse> sendTextToImageRequest(PromptSettings settings, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var request = txt2ImageRequestFromSettings(settings);

            if (_mainRequestCancellationSource?.IsCancellationRequested == false)
            {
                _mainRequestCancellationSource.Cancel();
            }

            _mainRequestCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var progressCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
            }, cancellationToken);

            ApiResponse? apiResponse = null;
            var skipCurrentImage = false;
            var finished = false;

            while (!finished)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

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
                        PopulateSeeds(generationResponse);

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
                    PopulateSeeds(generationResponse);

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

        private async IAsyncEnumerable<ApiResponse> sendImageToImageRequest(PromptSettings settings, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var request = image2ImageRequestFromSettings(settings);

            if (_mainRequestCancellationSource?.IsCancellationRequested == false)
            {
                _mainRequestCancellationSource.Cancel();
            }

            _mainRequestCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var progressCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
            }, cancellationToken);

            ApiResponse? apiResponse = null;
            var skipCurrentImage = true;
            var finished = false;

            while (!finished)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

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
                        PopulateSeeds(generationResponse);

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
                    PopulateSeeds(generationResponse);

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
            request.Width = (int)settings.Width;
            request.Height = (int)settings.Height;
            request.DenoisingStrength = settings.DenoisingStrength;
            request.Steps = settings.Steps;
            request.Seed = settings.Seed;
            request.Tiling = settings.EnableTiling;

            // Hires Fix
            request.EnableHr = settings.EnableUpscaling &&
                !string.IsNullOrEmpty(settings.Upscaler) &&
                settings.UpscaleLevel > 0 &&
                settings.UpscaleSteps > 0;

            if (request.EnableHr == true)
            {
                request.HrScale = settings.UpscaleLevel;
                request.HrSecondPassSteps = settings.UpscaleSteps;
                request.HrUpscaler = settings.Upscaler;

                var additionalModules = new List<UntypedNode>();

                if (!string.IsNullOrEmpty(settings.Vae) && settings.Vae != "Automatic" && settings.Vae != "None")
                {
                    if (_moduleVaes != null && _moduleVaes.Contains(settings.Vae))
                    {
                        additionalModules.Add(new UntypedString(settings.Vae));
                    }
                }

                if (!string.IsNullOrEmpty(settings.TextEncoder) && settings.TextEncoder != "None")
                {
                    additionalModules.Add(new UntypedString(settings.TextEncoder));
                }

                request.HrAdditionalModules = new UntypedArray(additionalModules);
            }
            

            var combinedPromptAndStyles = settings.GetCombinedPromptAndPromptStyles();
            request.Prompt = combinedPromptAndStyles.Prompt;
            request.NegativePrompt = combinedPromptAndStyles.NegativePrompt;

            request.SamplerName = settings.Sampler;

            System.Diagnostics.Debug.WriteLine($"[SdForgeNeoService] Generating image with ModelType: {settings.ModelType}");
            System.Diagnostics.Debug.WriteLine($"[SdForgeNeoService] Model: {settings.Model?.DisplayName}");
            System.Diagnostics.Debug.WriteLine($"[SdForgeNeoService] Steps: {settings.Steps}, CFG: {settings.GuidanceScale}, Sampler: {settings.Sampler}");

            request.Scheduler = settings.Scheduler;
            System.Diagnostics.Debug.WriteLine($"[SdForgeNeoService] Scheduler: {settings.Scheduler}");

            if (settings.ModelType == Enums.ModelType.ZImageTurbo || settings.ModelType == Enums.ModelType.Flux)
            {
                if (settings.DistilledCfgScale.HasValue)
                {
                    request.AdditionalData.Add("distilled_cfg_scale", settings.DistilledCfgScale.Value);
                    System.Diagnostics.Debug.WriteLine($"[SdForgeNeoService] DistilledCfgScale: {settings.DistilledCfgScale.Value}");
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
            request.Width = (int)settings.Width;
            request.Height = (int)settings.Height;
            request.DenoisingStrength = settings.DenoisingStrength;
            request.Steps = settings.Steps;
            request.Seed = settings.Seed;
            request.Tiling = settings.EnableTiling;

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
            request.MaskRound = false;

            request.Scheduler = settings.Scheduler;

            if (settings.ModelType == Enums.ModelType.ZImageTurbo || settings.ModelType == Enums.ModelType.Flux)
            {
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

        public Task<byte[]> GetImageBytesAsync(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url)) return Task.FromResult(Array.Empty<byte>());

            return Task.Run(async () =>
            {
                try
                {
                    using var client = _httpClientFactory.CreateClient();
                    return await client.GetByteArrayAsync(url, cancellationToken);
                }
                catch
                {
                    return Array.Empty<byte>();
                }
            }, cancellationToken);
        }
        /// <summary>
        /// Reads generation parameters from an image stream.
        /// Prioritizes the JSON format but falls back to Forge's text format.
        /// Attempts to resolve the full Model object from the currently loaded list.
        /// </summary>
        public async Task<PromptSettings?> GetImageInfoAsync(string base64EncodedImage, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(base64EncodedImage)) return null;

            try 
            {
                // Remove data uri prefix if present
                var base64Data = base64EncodedImage;
                if (base64Data.Contains(","))
                {
                    base64Data = base64Data.Split(',')[1];
                }

                var bytes = Convert.FromBase64String(base64Data);
                using var stream = new MemoryStream(bytes);
                
                var settings = await PngMetadataHelper.ReadSettingsFromStreamAsync(stream);

                if (settings != null)
                {
                    // Try to resolve the model model (which might just have Name/Key from JSON)
                    // against our actual loaded model list to ensure we have the full object
                    if (settings.Model != null && _models?.Any() == true)
                    {
                        // Match by Hash (Key) or Name
                        var matchingModel = _models.FirstOrDefault(m => 
                            (settings.Model.Key != null && m.Sha256 == settings.Model.Key) ||
                            (settings.Model.DisplayName != null && m.ModelName == settings.Model.DisplayName) ||
                            (settings.Model.DisplayName != null && m.Title == settings.Model.DisplayName)
                        );

                        if (matchingModel != null)
                        {
                            var modelVm = _serviceProvider.GetService<IModelViewModel>();
                            if (modelVm != null)
                            {
                                modelVm.DisplayName = matchingModel.ModelName ?? string.Empty;
                                modelVm.Key = matchingModel.Title ?? string.Empty;
                                settings.Model = modelVm;
                            }
                        }
                    }

                    if (settings.Model == null && _client != null)
                    {
                        settings.Model = await GetSelectedModelAsync(cancellationToken);
                    }
                }

                return settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse image info locally: {ex}");
                return null;
            }
        }

        public async Task RefreshResourcesAsync(CancellationToken cancellationToken = default)
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
                    using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    probeCts.CancelAfter(TimeSpan.FromSeconds(4));
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
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var tasks = new List<Task>
            {
                // Options is strictly required and fetched first/parallel
                Task.Run(async () => _options = await _client.Sdapi.V1.OptionsPath.GetAsync(cancellationToken: cts.Token), cancellationToken),
                Task.Run(async () =>
                {
                    try
                    {
                        _samplers = await _client.Sdapi.V1.Samplers.GetAsync(cancellationToken: cts.Token);
                    }
                    catch { /* Ignore if endpoint doesn't exist */ }
                }, cancellationToken),
                Task.Run(async () =>
                {
                    try
                    {
                        _schedulers = await _client.Sdapi.V1.Schedulers.GetAsync(cancellationToken: cts.Token);
                    }
                    catch { /* Ignore if endpoint doesn't exist */ }
                }, cancellationToken),
                Task.Run(async () => _models = await _client.Sdapi.V1.SdModels.GetAsync(cancellationToken: cts.Token), cancellationToken),
                Task.Run(async () =>
                {
                    try
                    {
                        var response = await _httpClientFactory.CreateClient().GetAsync($"{_baseUrl.TrimEnd('/')}/sdapi/v1/sd-modules", cts.Token);
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync(cts.Token);
                            var modules = JArray.Parse(json);
                            
                            _moduleVaes = new List<string>();
                            _textEncoders = new List<string>();

                            foreach (var module in modules)
                            {
                                var modelName = module["model_name"]?.ToString();
                                var filename = module["filename"]?.ToString();

                                if (!string.IsNullOrEmpty(modelName) && !string.IsNullOrEmpty(filename))
                                {
                                    if (filename.Contains("\\VAE\\", StringComparison.OrdinalIgnoreCase) || 
                                        filename.Contains("/VAE/", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _moduleVaes.Add(modelName);
                                    }
                                    else
                                    {
                                        _textEncoders.Add(modelName);
                                    }
                                }
                            }
                        }
                    }
                    catch { /* Ignore if endpoint doesn't exist */ }
                }, cancellationToken),
                Task.Run(async () =>
                {
                    try
                    {
                        var lorasNode = await _client.Sdapi.V1.Loras.GetAsync(cancellationToken: cts.Token);
                        _loras = ParseLoras(lorasNode);
                    }
                    catch { /* Ignore if endpoint doesn't exist */ }
                }, cancellationToken),
                Task.Run(async () =>
                {
                    try
                    {
                        _upscalers = await _client.Sdapi.V1.Upscalers.GetAsync(cancellationToken: cts.Token);
                    }
                    catch { /* Ignore if endpoint doesn't exist */ }
                }, cancellationToken)
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

        public Task<Dictionary<string, string>> GetSamplersAsync(CancellationToken cancellationToken = default)
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

        public Task<List<string>> GetSchedulersAsync(CancellationToken cancellationToken = default)
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

        public Task<List<IModelViewModel>> GetModelsAsync(CancellationToken cancellationToken = default)
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

        public Task<List<ILoraViewModel>> GetLorasAsync(CancellationToken cancellationToken = default)
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

        public Task<List<IUpscalerViewModel>> GetUpscalersAsync(CancellationToken cancellationToken = default)
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

        public Task<List<string>> GetVaesAsync(CancellationToken cancellationToken = default)
        {
            var result = new List<string> { "Automatic", "None" };

            if (_moduleVaes != null)
            {
                foreach (var vae in _moduleVaes)
                {
                    if (!result.Contains(vae))
                    {
                        result.Add(vae);
                    }
                }
            }

            return Task.FromResult(result);
        }

        public Task<List<string>> GetTextEncodersAsync(CancellationToken cancellationToken = default)
        {
            var result = new List<string> { "None" };

            if (_textEncoders != null)
            {
                foreach (var encoder in _textEncoders)
                {
                    if (!result.Contains(encoder))
                    {
                        result.Add(encoder);
                    }
                }
            }

            return Task.FromResult(result);
        }

        public Task<IModelViewModel?> GetSelectedModelAsync(CancellationToken cancellationToken = default)
        {
            var result = _serviceProvider.GetService<IModelViewModel>();

            if (_options == null || _models == null) return Task.FromResult<IModelViewModel?>(result);

            var currentModelTitle = GetOptionValue(_options.SdModelCheckpoint);
            var selectedModel = _models.FirstOrDefault(m => m.Title == currentModelTitle);

            if (selectedModel != null && result != null)
            {
                result.DisplayName = selectedModel.ModelName ?? string.Empty;
                result.Key = selectedModel.Title ?? string.Empty;
            }

            return Task.FromResult<IModelViewModel?>(result);
        }

        public async Task SaveSettingsAsync(PromptSettings settings, CancellationToken cancellationToken = default)
        {
            if (_client == null || settings == null) return;

            _options = await _client.Sdapi.V1.OptionsPath.GetAsync(cancellationToken: cancellationToken);
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
                    if (settings.ModelType == Mogri.Enums.ModelType.ZImageTurbo && settings.Loras?.Any() == true)
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

                    var additionalModules = new List<string>();

                    if (!string.IsNullOrEmpty(settings.Vae) && settings.Vae != "Automatic" && settings.Vae != "None")
                    {
                        // If the VAE is from the modules list, add it to additional_modules
                        if (_moduleVaes != null && _moduleVaes.Contains(settings.Vae))
                        {
                            additionalModules.Add(settings.Vae);
                            requestBody.AdditionalData.TryAdd("sd_vae", "Automatic"); // Reset sd_vae if using module
                        }
                        else
                        {
                            requestBody.AdditionalData.TryAdd("sd_vae", settings.Vae);
                        }
                    }
                    else
                    {
                        requestBody.AdditionalData.TryAdd("sd_vae", settings.Vae ?? "Automatic");
                    }

                    if (!string.IsNullOrEmpty(settings.TextEncoder) && settings.TextEncoder != "None")
                    {
                        additionalModules.Add(settings.TextEncoder);
                    }

                    requestBody.AdditionalData.TryAdd("forge_additional_modules", additionalModules);
                }
            }

            if (!requestBody.AdditionalData.Any())
            {
                return;
            }

            await _client.Sdapi.V1.OptionsPath.PostAsync(requestBody, cancellationToken: cancellationToken);

            await RefreshResourcesAsync(cancellationToken);
        }

        public async Task<bool> CancelAsync(CancellationToken cancellationToken = default)
        {
            if (_client == null) return false;
            try
            {
                await _client.Sdapi.V1.Interrupt.PostAsync(cancellationToken: cancellationToken);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void PopulateSeeds(GenerationResponse response)
        {
            if (string.IsNullOrEmpty(response.Info))
            {
                return;
            }

            try
            {
                var infoObject = JsonConvert.DeserializeObject<JObject>(response.Info);

                if (infoObject != null)
                {
                    if (infoObject.ContainsKey("all_seeds"))
                    {
                        response.Seeds = infoObject["all_seeds"]?.ToObject<List<long>>();
                        return;
                    }

                    if (infoObject.ContainsKey("seed"))
                    {
                        var seedToken = infoObject["seed"];
                        if (seedToken != null)
                        {
                            var seed = seedToken.ToObject<long>();
                            response.Seeds = new List<long> { seed };
                            return;
                        }
                    }
                }
            }
            catch
            {
                // Ignore json errors
            }

            if (long.TryParse(response.Info, out var parsedSeed))
            {
                response.Seeds = new List<long> { parsedSeed };
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

        private static string? StripModelHash(string? model)
        {
            if (string.IsNullOrEmpty(model))
            {
                return model;
            }

            var hashStartIndex = model.LastIndexOf(" [", StringComparison.Ordinal);
            if (hashStartIndex < 0 || !model.EndsWith("]", StringComparison.Ordinal))
            {
                return model;
            }

            var hashLength = model.Length - hashStartIndex - 3;
            if (hashLength <= 0)
            {
                return model;
            }

            for (var i = hashStartIndex + 2; i < model.Length - 1; i++)
            {
                if (!Uri.IsHexDigit(model[i]))
                {
                    return model;
                }
            }

            return model[..hashStartIndex];
        }

        public Task<ModelType> GetCurrentModelTypeAsync(CancellationToken cancellationToken = default)
        {
            if (_options == null)
            {
                return Task.FromResult(ModelType.SDXL);
            }

            var currentModel = GetOptionValue(_options.SdModelCheckpoint);
            if (string.IsNullOrEmpty(currentModel))
            {
                return Task.FromResult(ModelType.SDXL);
            }

            var normalizedCurrentModel = StripModelHash(currentModel);

            if (normalizedCurrentModel == StripModelHash(GetOptionValue(_options.ForgeCheckpointSd)))
            {
                return Task.FromResult(ModelType.SD15);
            }
            if (normalizedCurrentModel == StripModelHash(GetOptionValue(_options.ForgeCheckpointXl)))
            {
                return Task.FromResult(ModelType.SDXL);
            }
            if (normalizedCurrentModel == StripModelHash(GetOptionValue(_options.ForgeCheckpointZit)))
            {
                return Task.FromResult(ModelType.ZImageTurbo);
            }
            if (normalizedCurrentModel == StripModelHash(GetOptionValue(_options.ForgeCheckpointFlux)))
            {
                return Task.FromResult(ModelType.Flux);
            }

            return Task.FromResult(ModelType.SDXL);
        }
    }
}
