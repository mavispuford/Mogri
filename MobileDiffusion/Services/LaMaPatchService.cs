using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using MobileDiffusion.Interfaces.Services;
using SkiaSharp;

namespace MobileDiffusion.Services
{
    public class LaMaPatchService : IPatchService
    {
        private InferenceSession _session;
        private const int ModelInputSize = 512;
        private bool _isLoading = false;

        public LaMaPatchService()
        {
            // Deliberately not initializing here so it doesn't happen at page load
        }

        private async Task InitializeModelAsync()
        {
            if (_session != null || _isLoading) return;
            _isLoading = true;
            Console.WriteLine("[LaMaPatchService] InitializeModelAsync started.");

            try
            {
                // To save memory, we extract the model to a temp file and load from path 
                // instead of loading a huge byte[] into managed memory.
                var modelPath = Path.Combine(FileSystem.CacheDirectory, "lama_int8.onnx");
                
                if (!File.Exists(modelPath))
                {
                    Console.WriteLine("[LaMaPatchService] Extracting model to cache...");
                    using var stream = await FileSystem.OpenAppPackageFileAsync("lama_int8.onnx");
                    using var fileStream = File.Create(modelPath);
                    await stream.CopyToAsync(fileStream);
                    Console.WriteLine($"[LaMaPatchService] Model extracted to: {modelPath}");
                }
                else
                {
                    Console.WriteLine($"[LaMaPatchService] Model already exists at: {modelPath}");
                }
                
                var options = new SessionOptions();
                // Explicitly define CPU usage to avoid potential NNAPI stability issues on Android
                // useArena: 0 (False) prevents pre-allocating large memory chunks, reducing peak RAM usage (at cost of speed)
                options.AppendExecutionProvider_CPU(useArena: 0); 
                options.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_VERBOSE;

                // Limit threads to prevent overheating
                options.InterOpNumThreads = 2;
                options.IntraOpNumThreads = 2;
                
                // Disable all graph optimizations to prevent memory spikes during graph rewriting
                options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_DISABLE_ALL;

                // Load from file path (Native C++ loads it, avoiding Managed Heap allocation)
                _session = new InferenceSession(modelPath, options);
                Console.WriteLine("[LaMaPatchService] InferenceSession created successfully.");
                
                // Log input metadata for debugging
                foreach (var input in _session.InputMetadata)
                {
                    Console.WriteLine($"[LaMaPatchService] Input: {input.Key}, Type: {input.Value.ElementType}, dimensions: {string.Join(",", input.Value.Dimensions)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LaMaPatchService] Failed to load LaMa model: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                _isLoading = false;
            }
        }

        public void UnloadModel()
        {
            Console.WriteLine("[LaMaPatchService] Unloading model...");
            try
            {
                _session?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LaMaPatchService] Error disposing session: {ex}");
            }
            finally
            {
                _session = null;
                GC.Collect();
            }
        }

        public async Task<SKBitmap> PatchImageAsync(SKBitmap image, SKBitmap mask)
        {
            Console.WriteLine("[LaMaPatchService] PatchImageAsync started");
            try
            {
                if (_session == null)
                {
                    Console.WriteLine("[LaMaPatchService] Session is null, initializing...");
                    await InitializeModelAsync();
                    if (_session == null) 
                    {
                        Console.WriteLine("[LaMaPatchService] Failed to initialize session");
                        throw new InvalidOperationException("Model not loaded");
                    }
                }

                // 1. Resize inputs to 512x512
                Console.WriteLine("[LaMaPatchService] Resizing inputs to 512x512...");
                // Enforce RGBA 8888 for consistent byte access
                using var resizedImage = image.Resize(new SKImageInfo(ModelInputSize, ModelInputSize, SKColorType.Rgba8888), SKSamplingOptions.Default);
                using var resizedMask = mask.Resize(new SKImageInfo(ModelInputSize, ModelInputSize, SKColorType.Rgba8888), SKSamplingOptions.Default);

                if (resizedImage == null || resizedMask == null)
                {
                    Console.WriteLine("[LaMaPatchService] Resize failed (result is null)");
                    throw new Exception("Failed to resize inputs");
                }
                Console.WriteLine("[LaMaPatchService] Inputs resized");

                // 2. Prepare Tensors
                Console.WriteLine("[LaMaPatchService] Preparing tensors...");
                var imageTensor = new DenseTensor<float>(new[] { 1, 3, ModelInputSize, ModelInputSize });
                var maskTensor = new DenseTensor<float>(new[] { 1, 1, ModelInputSize, ModelInputSize });

                // Iterate pixels
                // Using GetPixel for simplicity for now
                for (int y = 0; y < ModelInputSize; y++)
                {
                    for (int x = 0; x < ModelInputSize; x++)
                    {
                        var pixel = resizedImage.GetPixel(x, y);
                        var maskPixel = resizedMask.GetPixel(x, y);

                        // Normalize image to [0, 1]
                        imageTensor[0, 0, y, x] = pixel.Red / 255.0f;
                        imageTensor[0, 1, y, x] = pixel.Green / 255.0f;
                        imageTensor[0, 2, y, x] = pixel.Blue / 255.0f;

                        // Normalize mask. Assuming white (255) is the hole to fill.
                        var maskVal = maskPixel.Red > 127 ? 1.0f : 0.0f; 
                        maskTensor[0, 0, y, x] = maskVal;
                    }
                }
                Console.WriteLine("[LaMaPatchService] Tensors prepared");

                // 3. Run Inference
                Console.WriteLine("[LaMaPatchService] Creating Onnx inputs...");

                // Force a GC before allocating input wrappers and running inference
                // to clear out the large bitmaps from the resize step (resizing creates copies).
                GC.Collect();
                GC.WaitForPendingFinalizers();

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("image", imageTensor),
                    NamedOnnxValue.CreateFromTensor("mask", maskTensor)
                };

                // Run on a background thread to avoid blocking UI
                // InferenceSession.Run is blocking
                Console.WriteLine("[LaMaPatchService] Running inference (background thread)...");
                
                using var results = await Task.Run(() => 
                {
                    // Double check Inputs before running (Native crash prevention)
                    if (inputs == null || inputs.Count != 2) throw new InvalidOperationException("Inputs invalid");
                    
                    return _session.Run(inputs);
                });
                
                Console.WriteLine("[LaMaPatchService] Inference returned results");
                
                var outputTensor = results.First().AsTensor<float>();
                Console.WriteLine("[LaMaPatchService] Got output tensor");

                // 4. Convert output to SKBitmap
                Console.WriteLine("[LaMaPatchService] Converting tensor to SKBitmap...");
                var outputBitmap = new SKBitmap(ModelInputSize, ModelInputSize, SKColorType.Rgba8888, SKAlphaType.Premul);
                for (int y = 0; y < ModelInputSize; y++)
                {
                    for (int x = 0; x < ModelInputSize; x++)
                    {
                        var r = (byte)(Math.Clamp(outputTensor[0, 0, y, x], 0f, 1f) * 255);
                        var g = (byte)(Math.Clamp(outputTensor[0, 1, y, x], 0f, 1f) * 255);
                        var b = (byte)(Math.Clamp(outputTensor[0, 2, y, x], 0f, 1f) * 255);
                        
                        outputBitmap.SetPixel(x, y, new SKColor(r, g, b));
                    }
                }
                Console.WriteLine("[LaMaPatchService] Output bitmap created");

                // 5. Resize back to original
                Console.WriteLine($"[LaMaPatchService] Resizing output back to {image.Width}x{image.Height}...");
                var finalOutput = outputBitmap.Resize(new SKImageInfo(image.Width, image.Height), SKSamplingOptions.Default);
                outputBitmap.Dispose();

                // 6. Composite
                Console.WriteLine("[LaMaPatchService] Compositing final image...");
                // We return a copy of the original image, with the patch applied ONLY where the mask is white.
                var result = image.Copy();
                
                using (var resultCanvas = new SKCanvas(result))
                {
                    // Create a layer to apply the mask
                    resultCanvas.SaveLayer(new SKPaint());
                    
                    // Draw the inpainted result
                    resultCanvas.DrawBitmap(finalOutput, 0, 0);
                    
                    // Draw mask with DstIn to keep only the inpainted parts where mask exists
                    using var maskPaint = new SKPaint();
                    maskPaint.BlendMode = SKBlendMode.DstIn;
                    
                    // IMPORTANT: The mask is White (Inpaint) and Black (Keep). 
                    // Since it has no Alpha transparency in the Black areas, we must use LumaColor
                    // to convert Black (Luma 0) to Transparent (Alpha 0) and White (Luma 1) to Opaque (Alpha 1).
                    maskPaint.ColorFilter = SKColorFilter.CreateLumaColor();
                    
                    resultCanvas.DrawBitmap(mask, 0, 0, maskPaint);
                    
                    resultCanvas.Restore();
                }

                finalOutput.Dispose();
                
                Console.WriteLine("[LaMaPatchService] PatchImageAsync completed successfully");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LaMaPatchService] ERROR in PatchImageAsync: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw;
            }
        }
    }
}
