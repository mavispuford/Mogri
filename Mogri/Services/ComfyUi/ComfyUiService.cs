using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Mogri.Clients.ComfyUi;
using Mogri.Clients.ComfyUi.Models;
using Mogri.Enums;
using Mogri.Helpers;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;
using Mogri.Services.ComfyUi;
using Mogri.ViewModels;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Newtonsoft.Json.Linq;
using PromptRequest = Mogri.Clients.ComfyUi.Models.PromptRequest;
using PromptRequest_prompt = Mogri.Clients.ComfyUi.Models.PromptRequest_prompt;

namespace Mogri.Services;

/// <summary>
/// Implementation of IImageGenerationBackend for ComfyUI.
/// Handles the full lifecycle: constructing workflows, submitting via HTTP,
/// listening for progress via WebSocket, and retrieving resulting images.
/// </summary>
public class ComfyUiService : IImageGenerationBackend
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private ComfyUiClient? _client;
    private HttpClient? _httpClient;
    private string? _baseUrl;
    private string? _apiKey;
    
    // Cached resources
    private List<IModelViewModel> _models = new();
    private List<string> _checkpointModels = new();
    private List<string> _diffusionModels = new();
    private Dictionary<string, string> _samplers = new();
    private List<string> _schedulers = new();
    private List<string> _vaes = new();
    private List<string> _textEncoders = new();
    private List<string> _upscalerModels = new();
    private List<ILoraViewModel> _loras = new();

    private readonly object _softMaskCacheSync = new();
    private readonly SemaphoreSlim _softMaskProcessingGate = new(1, 1);
    private string? _softMaskCacheKey;
    private int _softMaskCacheBlurRadius;
    private byte[]? _softMaskCacheBytes;

    public virtual string Name => "ComfyUI";
    public bool Initialized { get; private set; }
    
    public BackendCapabilities Capabilities => new()
    {
        SupportsSeamless = false,
        SupportsUpscaling = true,
        SupportsConfigurableUpscaleScale = false,
        SupportsHiresFix = false,
        SupportsDistilledCfgScale = true,
        SupportsSamplerList = true,
        SupportsCancellation = true,
        SupportsLoras = true,
        SupportsSchedulers = true,
        SupportsVaes = true,
        SupportsTextEncoders = true
    };

    public ComfyUiService(IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider)
    {
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Initialized = false;

        try
        {
            if (Name == "Comfy Cloud")
            {
                _baseUrl = "https://cloud.comfy.org";
            }
            else
            {
                _baseUrl = Preferences.Get(Constants.PreferenceKeys.ServerUrl, "http://127.0.0.1:8188");
            }
            
            _apiKey = Preferences.Get(Constants.PreferenceKeys.ComfyCloudApiKey, string.Empty);

            // 1. Create wrapper HttpClient
            _httpClient = _httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri(_baseUrl);

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MogriApp/1.0");
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
            }

            var customAuthName = Preferences.Default.Get(Constants.PreferenceKeys.AuthHeaderName, string.Empty);
            var customAuthValue = Preferences.Default.Get(Constants.PreferenceKeys.AuthHeaderValue, string.Empty);

            if (!string.IsNullOrWhiteSpace(customAuthName) && !string.IsNullOrWhiteSpace(customAuthValue))
            {
                _httpClient.DefaultRequestHeaders.Add(customAuthName, customAuthValue);
            }

            // 2. Create Kiota Client
            // Since we need custom headers/base url, we can use the HttpClientAdapter
            var authProvider = new AnonymousAuthenticationProvider(); // We handle auth in HttpClient or manual headers
            var adapter = new HttpClientRequestAdapter(authProvider, httpClient: _httpClient);
            adapter.BaseUrl = _baseUrl; // Ensure BaseUrl is set on adapter too
            
            _client = new ComfyUiClient(adapter);

            // 3. Refresh Resources
            await RefreshResourcesAsync(cancellationToken);

            Initialized = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ComfyUI Initialization Failed: {ex}");
            Console.WriteLine($"ComfyUI Initialization Failed: {ex}");
            Initialized = false;
            throw;
        }
    }

    public async Task RefreshResourcesAsync(CancellationToken cancellationToken = default)
    {
        if (_httpClient == null) return;

        try
        {
            // Fetch object info directly to parse dynamic JSON structure
            // GET /api/object_info
            var response = await _httpClient.GetAsync("/api/object_info", cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = $"Failed to get object info ({(int)response.StatusCode}).";
                try
                {
                    var errorJson = JObject.Parse(content);
                    if (errorJson["message"] != null)
                    {
                        errorMessage = $"ComfyUI Error: {errorJson["message"]}";
                    }
                }
                catch
                {
                    errorMessage += $" Response: {content}";
                }
                
                throw new Exception(errorMessage);
            }

            var json = JObject.Parse(content);

            // Models from integrated checkpoints and standalone diffusion models.
            _models.Clear();
            _checkpointModels.Clear();
            _diffusionModels.Clear();
            if (json["CheckpointLoaderSimple"]?["input"]?["required"]?["ckpt_name"] is JArray modelList)
            {
                // Enum values are usually the first element of the array [[values], default]
                if (modelList.First is JArray models)
                {
                    foreach (var model in models)
                    {
                        var name = model.ToString();
                        _checkpointModels.Add(name);
                        _models.Add(new ModelViewModel 
                        { 
                            DisplayName = name, 
                            Key = name,
                        });
                    }
                }
            }

            if (json["UNETLoader"]?["input"]?["required"]?["unet_name"] is JArray diffusionModelList &&
                diffusionModelList.First is JArray diffusionModels)
            {
                foreach (var diffusionModel in diffusionModels)
                {
                    var name = diffusionModel.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    _diffusionModels.Add(name);
                    if (!_models.Any(model => string.Equals(model.Key, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        _models.Add(new ModelViewModel
                        {
                            DisplayName = name,
                            Key = name
                        });
                    }
                }
            }

            // Samplers (KSampler)
            _samplers.Clear();
            if (json["KSampler"]?["input"]?["required"]?["sampler_name"] is JArray samplerList)
            {
                if (samplerList.First is JArray samplers)
                {
                    foreach (var s in samplers)
                    {
                        var name = s.ToString();
                        _samplers[name] = name;
                    }
                }
            }

            // Schedulers (KSampler)
            _schedulers.Clear();
            if (json["KSampler"]?["input"]?["required"]?["scheduler"] is JArray schedulerList)
            {
                if (schedulerList.First is JArray schedulers)
                {
                    foreach (var s in schedulers)
                    {
                        _schedulers.Add(s.ToString());
                    }
                }
            }

            // VAEs (VAELoader)
            _vaes.Clear();
            if (json["VAELoader"]?["input"]?["required"]?["vae_name"] is JArray vaeList)
            {
                if (vaeList.First is JArray vaes)
                {
                    foreach (var vae in vaes)
                    {
                        var name = vae.ToString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            _vaes.Add(name);
                        }
                    }
                }
            }

            // Text encoders (CLIPLoader)
            _textEncoders.Clear();
            if (json["CLIPLoader"]?["input"]?["required"]?["clip_name"] is JArray textEncoderList)
            {
                if (textEncoderList.First is JArray textEncoders)
                {
                    foreach (var textEncoder in textEncoders)
                    {
                        var name = textEncoder.ToString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            _textEncoders.Add(name);
                        }
                    }
                }
            }

            _upscalerModels.Clear();
            _upscalerModels.AddRange(ComfyUiResourceHelper.ParseUpscalerModelNames(content));

            // LoRAs (LoraLoader)
            _loras.Clear();
            if (json["LoraLoader"]?["input"]?["required"]?["lora_name"] is JArray loraList)
            {
                if (loraList.First is JArray loras)
                {
                    foreach (var l in loras)
                    {
                        var name = l.ToString();
                        _loras.Add(new LoraViewModel 
                        { 
                            Name = name,
                            Alias = name 
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to refresh resources: {ex.Message}");
            Console.WriteLine($"Failed to refresh resources: {ex.Message}");
            
            throw;
        }
    }

    public async Task<bool> CheckServerAsync(CancellationToken cancellationToken = default)
    {
        if (_httpClient == null || _client == null) return false;

        try
        {
            // Use Kiota client to check system stats
            await _client.Api.System_stats.GetAsync(cancellationToken: cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async IAsyncEnumerable<ApiResponse> SubmitImageRequestAsync(PromptSettings settings, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!Initialized || _httpClient == null || _client == null || _baseUrl == null)
            throw new InvalidOperationException("ComfyUiService not initialized");

        var isDirectSourceRequest = IsDirectSourceRequest(settings);
        if (settings.EnableUpscaling)
        {
            ResolveUpscaler(settings);
        }

        if (!isDirectSourceRequest)
        {
            ResolveModelResources(settings);
            ResolveSamplingSettings(settings);
        }

        string? imageFilename = null;
        string? maskFilename = null;
        string mode = "txt2img";

        // 1. Upload Init Image if needed
        if (!string.IsNullOrWhiteSpace(settings.InitImage))
        {
            mode = "img2img";
            imageFilename = await UploadImageAsync(settings.InitImage, cancellationToken);
            
            // Upload Mask if needed
            if (!isDirectSourceRequest && !string.IsNullOrWhiteSpace(settings.Mask))
            {
                mode = "inpaint";
                maskFilename = await UploadImageAsync(settings.Mask, cancellationToken, settings.MaskBlur);
            }
        }

        // 2. Build Workflow
        Dictionary<string, object> workflow;
        long seed = -1;
        if (mode == "inpaint" && imageFilename != null && maskFilename != null)
        {
            (workflow, seed) = ComfyUiWorkflowBuilder.BuildInpaintingWorkflow(
                settings,
                imageFilename,
                maskFilename,
                _diffusionModels);
        }
        else if (mode == "img2img" && imageFilename != null)
        {
            (workflow, seed) = ComfyUiWorkflowBuilder.BuildImageToImageWorkflow(
                settings,
                imageFilename,
                _diffusionModels);
        }
        else
        {
            (workflow, seed) = ComfyUiWorkflowBuilder.BuildTextToImageWorkflow(settings, _diffusionModels);
        }

        // 3. Connect to WebSocket
        var clientId = Guid.NewGuid().ToString();
        var wsClient = new ComfyUiWebSocketClient(_baseUrl, clientId, _apiKey);
        await wsClient.ConnectAsync(cancellationToken);
        
        // 4. Submit Workflow
        PromptRequest promptRequest = new PromptRequest 
        { 
             Prompt = new PromptRequest_prompt 
             {
                 AdditionalData = workflow
             },
             AdditionalData = new Dictionary<string, object> 
             {
                 { "client_id", clientId }
             }
        };

        string? promptId = null;
        try
        {
            var response = await _client.Api.Prompt.PostAsync(promptRequest, cancellationToken: cancellationToken);
            promptId = response?.PromptId?.ToString();
        }
        catch (Microsoft.Kiota.Abstractions.ApiException apiEx)
        {
            var errorContent = "ComfyUI API Error";
            if (apiEx.ResponseStatusCode == 400)
            {
                 errorContent = $"Invalid Workflow (400): {apiEx.Message}";
            }
            throw new Exception(errorContent, apiEx);
        }
        catch (Exception ex)
        {
             Debug.WriteLine($"Failed to submit workflow: {ex.Message}");
             throw new Exception($"Failed to submit workflow: {ex.Message}", ex);
        }

        if (string.IsNullOrEmpty(promptId))
        {
            throw new Exception("Failed to submit workflow: No prompt_id returned.");
        }

        // 5. Listen for Progress
        // Note: We cannot use try-catch around yield return. Exceptions will bubble up to caller.
        bool receivedFinalResponse = false;
        bool isInterrupted = false;
        
        // We can capture the enumerator to handle clean-up if needed, but simple foreach is fine.
        // If an exception occurs in Listener, it will propagate.
        
        await foreach (var progress in wsClient.ListenForPromptAsync(promptId, cancellationToken))
        {
            if (progress.ResponseObject is ProgressResponse progResponse)
            {
               if (progResponse.IsInterrupted)
               {
                   isInterrupted = true;
               }
            }

            // If completed, we need to download images before yielding
            if (progress.ResponseObject is GenerationResponse genResponse)
            {
                receivedFinalResponse = true;
                // Include the seed used
                genResponse.Seeds = new List<long> { seed };

                if (genResponse.Images != null && genResponse.Images.Count > 0)
                {
                    var loadedImages = new List<string>();
                    foreach (var filename in genResponse.Images)
                    {
                        var base64 = await DownloadImageAsBase64Async(filename, cancellationToken);
                        if (base64 != null)
                        {
                            loadedImages.Add(base64);
                        }
                    }
                    
                    genResponse.Images = loadedImages;
                }
            }
            
            yield return progress;
        }

        if (!receivedFinalResponse && !isInterrupted && !cancellationToken.IsCancellationRequested)
        {
            throw new Exception("Connection closed unexpectedly before generation completed.");
        }
    }

    private void ResolveUpscaler(PromptSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Upscaler))
        {
            throw new InvalidOperationException(
                "ComfyUI upscaling is enabled, but no upscaler model is selected. Select an upscaler before submitting.");
        }

        var requestedUpscaler = settings.Upscaler;
        var resolvedUpscaler = ComfyUiResourceHelper.FindUpscalerModelName(_upscalerModels, requestedUpscaler);
        if (resolvedUpscaler == null)
        {
            throw new InvalidOperationException(
                $"ComfyUI upscaler '{requestedUpscaler}' is not available. Refresh resources and verify the model is installed under the ComfyUI upscale_models folder or server configuration.");
        }

        settings.Upscaler = resolvedUpscaler;
    }

    private static bool IsDirectSourceRequest(PromptSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.InitImage) && settings.DenoisingStrength <= 0;
    }

    private void ResolveModelResources(PromptSettings settings)
    {
        if (settings.ModelType is ModelType.SD15 or ModelType.SDXL)
        {
            settings.Vae = null;
            settings.TextEncoder = null;
            settings.TextEncoderSecondary = null;
            return;
        }

        switch (settings.ModelType)
        {
            case ModelType.Krea2Turbo:
            case ModelType.Krea2Raw:
                ResolveKreaResources(settings);
                return;
            case ModelType.ZImageTurbo:
                ResolveZImageResources(settings);
                return;
            case ModelType.Flux:
                ResolveFluxResources(settings);
                return;
        }

        if (HasConcreteResource(settings.Vae))
        {
            settings.Vae = ModelResourceHelper.FindMatch(_vaes, settings.Vae)
                ?? throw new InvalidOperationException($"ComfyUI does not expose the selected VAE '{settings.Vae}'.");
        }
    }

    private void ResolveSamplingSettings(PromptSettings settings)
    {
        if (_samplers.Count > 0)
        {
            settings.Sampler = ComfyUiSamplingHelper.FindSampler(_samplers.Keys, settings.Sampler)
                ?? _samplers.Keys.First();
        }
        else if (string.IsNullOrWhiteSpace(settings.Sampler))
        {
            settings.Sampler = "euler";
        }

        if (_schedulers.Count > 0)
        {
            settings.Scheduler = ComfyUiSamplingHelper.FindScheduler(_schedulers, settings.Scheduler)
                ?? _schedulers.First();
        }
        else if (string.IsNullOrWhiteSpace(settings.Scheduler))
        {
            settings.Scheduler = "normal";
        }
    }

    private void ResolveKreaResources(PromptSettings settings)
    {
        EnsureStandaloneModelIfNeeded(settings);

        var textEncoder = ModelResourceHelper.FindMatch(_textEncoders, settings.TextEncoder);
        if (!ModelResourceHelper.IsKreaTextEncoder(textEncoder))
        {
            textEncoder = _textEncoders.FirstOrDefault(ModelResourceHelper.IsKreaTextEncoder);
        }

        if (textEncoder == null)
        {
            throw new InvalidOperationException(
                "ComfyUI has no compatible Krea text encoder. Refresh the server resources and select a Qwen3-VL 4B encoder.");
        }

        var vae = _vaes.FirstOrDefault(value =>
            string.Equals(value, "qwen_image_vae.safetensors", StringComparison.OrdinalIgnoreCase));
        if (vae == null)
        {
            throw new InvalidOperationException(
                "ComfyUI has no compatible Krea VAE. Install qwen_image_vae.safetensors and refresh the server resources.");
        }

        settings.TextEncoder = textEncoder;
        settings.Vae = vae;
    }

    private void ResolveZImageResources(PromptSettings settings)
    {
        EnsureModelIsAvailable(settings);

        settings.TextEncoder = _textEncoders.FirstOrDefault(ModelResourceHelper.IsZImageTextEncoder)
            ?? throw new InvalidOperationException(
                "ComfyUI has no compatible Z-Image Qwen3 4B text encoder. Refresh the server resources and select one.");
        settings.Vae = ModelResourceHelper.FindMatch(_vaes, "ae.safetensors")
            ?? ModelResourceHelper.FindMatch(_vaes, settings.Vae)
            ?? throw new InvalidOperationException(
                "ComfyUI has no compatible Z-Image VAE. Refresh the server resources and select ae.safetensors.");
    }

    private void ResolveFluxResources(PromptSettings settings)
    {
        EnsureModelIsAvailable(settings);

        var profile = GenerationProfile.GetDefault(ModelType.Flux);

        var primaryTextEncoder = ModelResourceHelper.FindMatch(_textEncoders, settings.TextEncoder);
        if (!ModelResourceHelper.IsFluxT5TextEncoder(primaryTextEncoder))
        {
            primaryTextEncoder = ModelResourceHelper.FindMatch(_textEncoders, "t5xxl");
        }

        var secondaryTextEncoder = ModelResourceHelper.FindMatch(_textEncoders, settings.TextEncoderSecondary);
        if (!ModelResourceHelper.IsFluxClipLTextEncoder(secondaryTextEncoder))
        {
            secondaryTextEncoder = ModelResourceHelper.FindMatch(_textEncoders, profile.DefaultTextEncoderSecondary);
        }

        settings.TextEncoder = primaryTextEncoder
            ?? throw new InvalidOperationException(
                "ComfyUI has no compatible Flux T5 text encoder. Refresh the server resources and select one.");
        settings.TextEncoderSecondary = secondaryTextEncoder
            ?? throw new InvalidOperationException(
                "ComfyUI has no compatible Flux CLIP-L text encoder. Refresh the server resources and select one.");
        settings.Vae = ModelResourceHelper.FindMatch(_vaes, "ae.safetensors")
            ?? throw new InvalidOperationException(
                "ComfyUI has no compatible Flux VAE. Refresh the server resources and select one.");
    }

    private void EnsureStandaloneModelIfNeeded(PromptSettings settings)
    {
        if (settings.ModelType is ModelType.Krea2Turbo or ModelType.Krea2Raw &&
            IsStandaloneKreaModel(settings.Model?.Key))
        {
            EnsureStandaloneModel(settings);
        }
    }

    private void EnsureModelIsAvailable(PromptSettings settings)
    {
        var modelKey = settings.Model?.Key;
        if (_diffusionModels.Any(model => string.Equals(model, modelKey, StringComparison.OrdinalIgnoreCase)) ||
            _checkpointModels.Any(model => string.Equals(model, modelKey, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        throw new InvalidOperationException(
            $"ComfyUI does not expose '{modelKey}' as a checkpoint or diffusion model. Refresh the server resources and verify the model path configuration.");
    }

    private void EnsureStandaloneModel(PromptSettings settings)
    {
        var modelKey = settings.Model?.Key;
        var diffusionModel = _diffusionModels.FirstOrDefault(model =>
            string.Equals(model, modelKey, StringComparison.OrdinalIgnoreCase));

        if (diffusionModel == null)
        {
            throw new InvalidOperationException(
                $"ComfyUI does not expose '{modelKey}' through UNETLoader. Register the model under diffusion_models and refresh resources.");
        }

        settings.Model!.Key = diffusionModel;
    }

    private static bool IsStandaloneKreaModel(string? modelKey)
    {
        return !string.IsNullOrWhiteSpace(modelKey) &&
            (modelKey.Contains("krea2_turbo_", StringComparison.OrdinalIgnoreCase) ||
             modelKey.Contains("krea2turbo_", StringComparison.OrdinalIgnoreCase) ||
             modelKey.Contains("krea2_raw_", StringComparison.OrdinalIgnoreCase) ||
             modelKey.Contains("krea2raw_", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasConcreteResource(string? resource)
    {
        return !string.IsNullOrWhiteSpace(resource) &&
            !string.Equals(resource, "Automatic", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(resource, "None", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> UploadImageAsync(string base64Image, CancellationToken cancellationToken, int? maskBlurRadius = null)
    {
        var maskCacheKey = base64Image;
        string extension = "png";
        string mimeType = "image/png";

        // Handle data URI scheme
        if (base64Image.StartsWith("data:"))
        {
            var metaEnd = base64Image.IndexOf(";base64,");
            if (metaEnd > 0)
            {
                var mime = base64Image.Substring(5, metaEnd - 5);
                if (mime == "image/jpeg" || mime == "image/jpg")
                {
                    extension = "jpg";
                    mimeType = "image/jpeg";
                }
                else if (mime == "image/webp")
                {
                    extension = "webp";
                    mimeType = "image/webp";
                }
                
                base64Image = base64Image.Substring(metaEnd + 8);
            }
            else if (base64Image.Contains(",")) 
            {
                 // Fallback for malformed data headers
                 base64Image = base64Image.Substring(base64Image.IndexOf(",") + 1);
            }
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64Image);
        }
        catch (FormatException ex)
        {
             Debug.WriteLine($"Invalid Base64 image data: {ex.Message}");
             throw new Exception("Invalid Base64 image data.");
        }

        if (maskBlurRadius is > 0)
        {
            var softenedMaskBytes = await GetSoftMaskBytesAsync(
                maskCacheKey,
                bytes,
                maskBlurRadius.Value,
                cancellationToken);
            if (softenedMaskBytes != null)
            {
                bytes = softenedMaskBytes;
                extension = "png";
                mimeType = "image/png";
            }
        }

        using var content = new MultipartFormDataContent();
        using var imageContent = new ByteArrayContent(bytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        
        // API expects "image" field
        var filename = $"upload_{Guid.NewGuid()}.{extension}";
        content.Add(imageContent, "image", filename);
        // ComfyUI's upload endpoint accepts input/temp/output directories; the workflow
        // determines whether an uploaded image is used as a source image or a mask.
        content.Add(new StringContent("input"), "type");
        
        var uploadResponse = await _httpClient!.PostAsync("/api/upload/image", content, cancellationToken);
        
        if (!uploadResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Image upload failed: {uploadResponse.ReasonPhrase}");
        }

        var json = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
        var result = JObject.Parse(json);
        
        return result["name"]?.ToString() ?? throw new Exception("Upload failed: no filename returned");
    }

    private async Task<byte[]?> GetSoftMaskBytesAsync(
        string cacheKey,
        byte[] imageBytes,
        int blurRadius,
        CancellationToken cancellationToken)
    {
        lock (_softMaskCacheSync)
        {
            if (string.Equals(_softMaskCacheKey, cacheKey, StringComparison.Ordinal) &&
                _softMaskCacheBlurRadius == blurRadius)
            {
                return _softMaskCacheBytes;
            }
        }

        await _softMaskProcessingGate.WaitAsync(cancellationToken);
        try
        {
            lock (_softMaskCacheSync)
            {
                if (string.Equals(_softMaskCacheKey, cacheKey, StringComparison.Ordinal) &&
                    _softMaskCacheBlurRadius == blurRadius)
                {
                    return _softMaskCacheBytes;
                }
            }

            var softenedMaskBytes = await Task.Run(
                () => ComfyUiMaskHelper.CreateSoftMaskPng(imageBytes, blurRadius),
                cancellationToken);

            if (softenedMaskBytes != null)
            {
                lock (_softMaskCacheSync)
                {
                    _softMaskCacheKey = cacheKey;
                    _softMaskCacheBlurRadius = blurRadius;
                    _softMaskCacheBytes = softenedMaskBytes;
                }
            }

            return softenedMaskBytes;
        }
        finally
        {
            _softMaskProcessingGate.Release();
        }
    }

    private async Task<string?> DownloadImageAsBase64Async(string filename, CancellationToken cancellationToken)
    {
        try
        {
            // GET /api/view?filename=...&type=output
            // Make sure we handle potential encoding/structure if filename has subfolder?
            // "filename" from executed event usually is just filename, unless subfolder is specified
            var response = await _httpClient!.GetAsync($"/api/view?filename={filename}&type=output", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                 var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                 return Convert.ToBase64String(bytes);
            }
            
            Console.WriteLine($"Image download failed for {filename}: {response.ReasonPhrase}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to download image: {filename}, {ex.Message}");
            Console.WriteLine($"Failed to download image: {filename}, {ex.Message}");
            return null; // Return null to skip this image but continue processing if others succeeded
        }
    }

    public async Task<byte[]> GetImageBytesAsync(string url, CancellationToken cancellationToken = default)
    {
        // If url is base64
        if (!url.StartsWith("http") && !url.StartsWith("/"))
        {
             return Convert.FromBase64String(url);
        }
        // If full url
        return await _httpClientFactory.CreateClient().GetByteArrayAsync(url, cancellationToken);
    }

    public async Task<PromptSettings?> GetImageInfoAsync(string base64EncodedImage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(base64EncodedImage)) return null;

        try
        {
            var base64Data = base64EncodedImage.Contains(',')
                ? base64EncodedImage.Split(',')[1]
                : base64EncodedImage;
            
            var bytes = Convert.FromBase64String(base64Data);
            using var stream = new MemoryStream(bytes);
            return await GetImageInfoAsync(stream, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task<PromptSettings?> GetImageInfoAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        if (imageStream == null)
        {
            return null;
        }

        try
        {
            if (imageStream.CanSeek)
            {
                imageStream.Position = 0;
            }

            return await PngMetadataHelper.ReadSettingsFromStreamAsync(imageStream);
        }
        catch
        {
            return null;
        }
    }
    
    // Resource Getters
    public Task<Dictionary<string, string>> GetSamplersAsync(CancellationToken cancellationToken = default) 
        => Task.FromResult(_samplers);

    public Task<List<string>> GetSchedulersAsync(CancellationToken cancellationToken = default) 
        => Task.FromResult(_schedulers);

    public Task<List<string>> GetVaesAsync(CancellationToken cancellationToken = default) 
        => Task.FromResult(_vaes);

    public Task<List<string>> GetTextEncodersAsync(CancellationToken cancellationToken = default) 
        => Task.FromResult(_textEncoders);

    public Task<List<IModelViewModel>> GetModelsAsync(CancellationToken cancellationToken = default) 
        => Task.FromResult(_models);

    public Task<List<ILoraViewModel>> GetLorasAsync(CancellationToken cancellationToken = default) 
        => Task.FromResult(_loras);

    public Task<List<IUpscalerViewModel>> GetUpscalersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_upscalerModels
            .Select(name => (IUpscalerViewModel)new UpscalerViewModel
            {
                Name = name,
                ModelName = name,
                Scale = 1.0
            })
            .ToList());

    public async Task<IModelViewModel?> GetSelectedModelAsync(CancellationToken cancellationToken = default)
    {
        if (_models.Count == 0)
        {
            return null;
        }

        var selectedModelKey = Preferences.Default.Get(GetSelectedModelPreferenceKey(), string.Empty);
        if (!string.IsNullOrWhiteSpace(selectedModelKey))
        {
            var selectedModel = _models.FirstOrDefault(m => m.Key == selectedModelKey);
            if (selectedModel != null)
            {
                return selectedModel;
            }
        }

        // Comfy backends do not expose an active server-side model selection endpoint.
        // Fall back to first known model when no persisted local selection exists.
        return _models.FirstOrDefault();
    }

    public Task<ModelType> GetCurrentModelTypeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult((ModelType)Preferences.Default.Get(Constants.PreferenceKeys.ComfyUiModelType, (int)ModelType.SDXL));
    }

    public Task SaveSettingsAsync(PromptSettings settings, CancellationToken cancellationToken = default)
    {
        Preferences.Default.Set(Constants.PreferenceKeys.ComfyUiModelType, (int)settings.ModelType);

        if (settings.Model != null && !string.IsNullOrWhiteSpace(settings.Model.Key))
        {
            Preferences.Default.Set(GetSelectedModelPreferenceKey(), settings.Model.Key);
        }

        return Task.CompletedTask;
    }

    private string GetSelectedModelPreferenceKey()
    {
        return Name == "Comfy Cloud"
            ? Constants.PreferenceKeys.ComfyCloudSelectedModel
            : Constants.PreferenceKeys.ComfyUiSelectedModel;
    }

    public async Task<bool> CancelAsync(CancellationToken cancellationToken = default)
    {
        if (_client == null) return false;
        try
        {
            await _client.Api.Interrupt.PostAsync(cancellationToken: cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
