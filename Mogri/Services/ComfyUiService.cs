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
    private Dictionary<string, string> _samplers = new();
    private List<string> _schedulers = new();
    private List<ILoraViewModel> _loras = new();

    public virtual string Name => "ComfyUI";
    public bool Initialized { get; private set; }
    
    public BackendCapabilities Capabilities => new()
    {
        SupportsSeamless = false,
        SupportsUpscaling = false, // Can add later
        SupportsSamplerList = true,
        SupportsCancellation = true,
        SupportsLoras = true,
        SupportsStyles = false,
        SupportsSchedulers = true
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

            // Models (CheckpointLoaderSimple)
            _models.Clear();
            if (json["CheckpointLoaderSimple"]?["input"]?["required"]?["ckpt_name"] is JArray modelList)
            {
                // Enum values are usually the first element of the array [[values], default]
                if (modelList.First is JArray models)
                {
                    foreach (var model in models)
                    {
                        var name = model.ToString();
                        _models.Add(new ModelViewModel 
                        { 
                            DisplayName = name, 
                            Key = name,
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

        string? imageFilename = null;
        string? maskFilename = null;
        string mode = "txt2img";

        // 1. Upload Init Image if needed
        if (!string.IsNullOrEmpty(settings.InitImage))
        {
            mode = "img2img";
            imageFilename = await UploadImageAsync(settings.InitImage, cancellationToken);
            
            // Upload Mask if needed
            if (!string.IsNullOrEmpty(settings.Mask))
            {
                mode = "inpaint";
                maskFilename = await UploadImageAsync(settings.Mask, cancellationToken, isMask: true);
            }
        }

        // 2. Build Workflow
        Dictionary<string, object> workflow;
        long seed = -1;
        if (mode == "inpaint" && imageFilename != null && maskFilename != null)
        {
            (workflow, seed) = ComfyUiWorkflowBuilder.BuildInpaintingWorkflow(settings, imageFilename, maskFilename);
        }
        else if (mode == "img2img" && imageFilename != null)
        {
            (workflow, seed) = ComfyUiWorkflowBuilder.BuildImageToImageWorkflow(settings, imageFilename);
        }
        else
        {
            (workflow, seed) = ComfyUiWorkflowBuilder.BuildTextToImageWorkflow(settings);
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

    private async Task<string> UploadImageAsync(string base64Image, CancellationToken cancellationToken, bool isMask = false)
    {
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

        using var content = new MultipartFormDataContent();
        using var imageContent = new ByteArrayContent(bytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        
        // API expects "image" field
        var filename = $"upload_{Guid.NewGuid()}.{extension}";
        content.Add(imageContent, "image", filename);
        content.Add(new StringContent(isMask ? "mask" : "input"), "type");
        // Note: For masks, some workflows expect 'input' type but use the image as a mask. 
        // Using 'mask' type uploads to the 'input' folder anyway but might be treated differently by the mask editor.
        // Let's stick to 'input' type based on testing unless specifically needing mask editor features.
        // Actually, let's keep 'input' for now as previously decided to be safe.
        // Reverting 'type' to 'input' for consistency.
        
        var uploadResponse = await _httpClient!.PostAsync("/api/upload/image", content, cancellationToken);
        
        if (!uploadResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Image upload failed: {uploadResponse.ReasonPhrase}");
        }

        var json = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
        var result = JObject.Parse(json);
        
        return result["name"]?.ToString() ?? throw new Exception("Upload failed: no filename returned");
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
            return await PngMetadataHelper.ReadSettingsFromStreamAsync(stream);
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
        => Task.FromResult(new List<string>());

    public Task<List<string>> GetTextEncodersAsync(CancellationToken cancellationToken = default) 
        => Task.FromResult(new List<string>());

    public Task<List<IModelViewModel>> GetModelsAsync(CancellationToken cancellationToken = default) 
        => Task.FromResult(_models);

    public Task<List<ILoraViewModel>> GetLorasAsync(CancellationToken cancellationToken = default) 
        => Task.FromResult(_loras);

    public Task<List<IUpscalerViewModel>> GetUpscalersAsync(CancellationToken cancellationToken = default) 
        => Task.FromResult(new List<IUpscalerViewModel>());

    public Task<List<IPromptStyleViewModel>> GetPromptStylesAsync(CancellationToken cancellationToken = default) 
        => Task.FromResult(new List<IPromptStyleViewModel>());

    public async Task<IModelViewModel?> GetSelectedModelAsync(CancellationToken cancellationToken = default)
    {
        // No server-side selection concept, so just return first model or null
        return _models.FirstOrDefault();
    }

    public Task<ModelType> GetCurrentModelTypeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult((ModelType)Preferences.Default.Get(Constants.PreferenceKeys.ComfyUiModelType, (int)ModelType.SDXL));
    }

    public Task SaveSettingsAsync(PromptSettings settings, CancellationToken cancellationToken = default)
    {
        Preferences.Default.Set(Constants.PreferenceKeys.ComfyUiModelType, (int)settings.ModelType);
        return Task.CompletedTask;
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
