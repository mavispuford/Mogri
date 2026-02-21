using System.Net.WebSockets;
using System.Text;
using MobileDiffusion.Models;
using Newtonsoft.Json.Linq;

namespace MobileDiffusion.Services.ComfyUi;

public class ComfyUiWebSocketClient
{
    private readonly string _baseUrl;
    private readonly string _clientId;
    private readonly string? _apiKey;
    private ClientWebSocket? _ws;

    public ComfyUiWebSocketClient(string baseUrl, string clientId, string? apiKey = null)
    {
        _baseUrl = baseUrl.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/');
        _clientId = clientId;
        _apiKey = apiKey;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_ws != null && _ws.State == WebSocketState.Open) return;

        _ws = new ClientWebSocket();
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _ws.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
            _ws.Options.SetRequestHeader("X-API-Key", _apiKey);
        }

        var uriBuilder = new UriBuilder($"{_baseUrl}/ws");
        var query = $"clientId={_clientId}";
        uriBuilder.Query = query;

        try
        {
            await _ws.ConnectAsync(uriBuilder.Uri, cancellationToken);
        }
        catch
        {
             throw; 
        }
    }

    public async IAsyncEnumerable<ApiResponse> ListenForPromptAsync(string promptId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_ws == null || _ws.State != WebSocketState.Open)
        {
             // Try to connect if not already connected (fallback)
             await ConnectAsync(cancellationToken);
        }

        var buffer = new byte[1024 * 1024]; // 1MB buffer
        var outputImages = new List<string>();

        while (_ws != null && _ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            var memoryStream = new MemoryStream();

            try
            {
                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    memoryStream.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                yield break;
            }

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(memoryStream.ToArray());
                
                JObject? json = null;
                try
                {
                    json = JObject.Parse(message);
                }
                catch
                {
                    continue; // Skip invalid JSON
                }

                var type = json["type"]?.ToString();
                var data = json["data"] as JObject;

                // Only process messages for our promptId
                // Note: 'status' message doesn't have prompt_id, it's global.
                // 'execution_start' has prompt_id
                // 'executing' doesn't always have prompt_id in data, check carefully
                // 'progress' has prompt_id
                
                // Common pattern check
                // data might be null for some messages
                
                if (type == "status")
                {
                    // Global status, containing queue size etc. Not specific to prompt.
                    // Could use for "waiting" state?
                    var status = data?["status"] as JObject;
                    var execInfo = status?["exec_info"] as JObject;
                    var queueRemaining = execInfo?["queue_remaining"]?.Value<int>() ?? 0;
                    
                    yield return new ApiResponse 
                    { 
                        ResponseObject = new ProgressResponse { Progress = 0, EtaRelative = queueRemaining },
                        Progress = 0
                    };
                    continue;
                }
                
                // Messages usually have { "type": "...", "data": { ... } }
                
                if (type == "execution_start")
                {
                    var msgPromptId = data?["prompt_id"]?.ToString();
                    if (msgPromptId == promptId)
                    {
                        yield return new ApiResponse 
                        { 
                            ResponseObject = new ProgressResponse { Progress = 0.01 },
                            Progress = 0.01 
                        };
                    }
                }
                else if (type == "progress")
                {
                    var msgPromptId = data?["prompt_id"]?.ToString();
                    if (msgPromptId == promptId)
                    {
                        var value = data?["value"]?.Value<double>() ?? 0;
                        var max = data?["max"]?.Value<double>() ?? 1;
                        var progress = Math.Min(value / max, 0.99);

                        yield return new ApiResponse 
                        { 
                            ResponseObject = new ProgressResponse { Progress = progress },
                            Progress = progress 
                        };
                    }
                }
                else if (type == "executing")
                {
                    var node = data?["node"]?.ToString();
                    var msgPromptId = data?["prompt_id"]?.ToString();
                    
                    if (msgPromptId == promptId)
                    {
                        if (string.IsNullOrEmpty(node))
                        {
                            // Execution finished!
                            yield return new ApiResponse 
                            { 
                                ResponseObject = new GenerationResponse { Images = outputImages },
                                Progress = 1.0 
                            };
                            yield break; // Done
                        }
                    }
                }
                else if (type == "executed")
                {
                    var msgPromptId = data?["prompt_id"]?.ToString();
                    if (msgPromptId == promptId)
                    {
                        var outputStr = data?["output"]?.ToString(); // "output": { "images": [ ... ] }
                        if (!string.IsNullOrEmpty(outputStr))
                        {
                            var outputObj = data?["output"] as JObject;
                            var images = outputObj?["images"] as JArray;
                            if (images != null)
                            {
                                foreach (var img in images)
                                {
                                    var filename = img["filename"]?.ToString();
                                    // Store full info if needed, but for now filename
                                    // We might need "subfolder" and "type" too for retrieval
                                    if (!string.IsNullOrEmpty(filename))
                                    {
                                        outputImages.Add(filename);
                                    }
                                }
                            }
                        }
                    }
                }
                else if (type == "execution_error")
                {
                    var msgPromptId = data?["prompt_id"]?.ToString();
                    if (msgPromptId == promptId)
                    {
                         // Throw or handle error
                         var exMsg = data?["exception_message"]?.ToString() ?? "Unknown error";
                         throw new Exception($"ComfyUI Error: {exMsg}");
                    }
                }
                 else if (type == "execution_interrupted")
                {
                    var msgPromptId = data?["prompt_id"]?.ToString();
                    if (msgPromptId == promptId)
                    {
                        // Yield partial results or just stop
                         yield return new ApiResponse 
                        { 
                            ResponseObject = new ProgressResponse { IsInterrupted = true },
                            Progress = 0 
                        };
                        // Should probably yield whatever images we have if any?
                        if (outputImages.Any())
                        {
                            yield return new ApiResponse 
                            { 
                                ResponseObject = new GenerationResponse { Images = outputImages },
                                Progress = 1.0 
                            };
                        }
                        yield break;
                    }
                }
            }
            else if (result.MessageType == WebSocketMessageType.Binary)
            {
                // Binary preview image
                // Format: 4 bytes event type (integer), followed by image data
                // Event type 1 = PREVIEW_IMAGE
                
                var bytes = memoryStream.ToArray();
                if (bytes.Length > 8) // Header check
                {
                     // First 4 bytes are big-endian integer for type
                     // But offset might be different (8 bytes prefix in some docs?)
                     // "Binary messages start with a 4-byte big-endian unsigned integer indicating the type of the binary message."
                     // 1: PREVIEW_IMAGE
                     // 2: UNCOMPRESSED_PREVIEW_IMAGE
                     
                     // Read first 4 bytes
                     int type = (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                     
                     if (type == 1) // PREVIEW_IMAGE (JPEG)
                     {
                         // Image data starts at offset 8? Or 4?
                         // "The visible binary message is: 4 bytes (type) + ... image data"
                         // Actually typically it's type (4) + image_type (4)? No, just type + data usually.
                         // Let's assume offset 4 or 8.
                         // But wait, the preview image also contains metadata?
                         // ComfyUI JS client: 
                         // const view = new DataView(event.data.slice(0, 4));
                         // const type = view.getUint32(0);
                         
                         // If type === 1:
                         // const imageBlob = new Blob([event.data.slice(8)], { type: 'image/jpeg' });
                         // So offset is 8.
                         
                         var imageData = new byte[bytes.Length - 8];
                         Array.Copy(bytes, 8, imageData, 0, imageData.Length);
                         var base64 = Convert.ToBase64String(imageData);
                         
                         yield return new ApiResponse
                         {
                             ResponseObject = new ProgressResponse 
                             { 
                                 Progress = 0, // Keep last progress? or just update image
                                 CurrentImage = base64 
                             }
                         };
                     }
                }
            }
        }
    }
}
