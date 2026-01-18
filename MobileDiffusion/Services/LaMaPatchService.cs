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
                
                // CRITICAL: Strict memory conservation for mobile devices
                // 1. Disable the memory arena. This prevents ORT from allocating huge chunks of RAM upfront.
                //    Instead, it mallocs/frees exactly what is needed for each operator. Slower, but safest for OOM.
                options.EnableCpuMemArena = false;
                
                // 2. Sequential execution. prevents parallel branches from allocating memory simultaneously.
                options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                
                // 3. Single thread. Reduces thread-pool overhead and context switching memory costs.
                options.InterOpNumThreads = 1;
                options.IntraOpNumThreads = 1;

                // 4. Basic optimizations only. 'All' can sometimes increase memory if it fuses nodes into larger kernels.
                options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_BASIC;
                
                options.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_VERBOSE;

                // Load from file path
                _session = new InferenceSession(modelPath, options);
                Console.WriteLine("[LaMaPatchService] InferenceSession created successfully.");
                
                // Log input metadata
                foreach (var input in _session.InputMetadata)
                {
                    Console.WriteLine($"[LaMaPatchService] Input: {input.Key}, Type: {input.Value.ElementType}, dimensions: {string.Join(",", input.Value.Dimensions)}");
                }
                foreach (var output in _session.OutputMetadata)
                {
                    Console.WriteLine($"[LaMaPatchService] Output: {output.Key}, Type: {output.Value.ElementType}, dimensions: {string.Join(",", output.Value.Dimensions)}");
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

                // 1. Get Bounding Box since 4K resizing is destructive
                Console.WriteLine("[LaMaPatchService] Calculating ROI...");
                var bbox = GetBoundingBox(mask);
                if (bbox.IsEmpty)
                {
                    Console.WriteLine("[LaMaPatchService] Empty mask, returning original image.");
                    return image.Copy();
                }
                
                // 2. Expand to Square Crop
                var cropRect = GetExpandedCropRect(bbox, image.Width, image.Height);
                Console.WriteLine($"[LaMaPatchService] ROI: {bbox}, Crop: {cropRect}");
                
                // 3. Crop Image and Mask
                using var croppedImage = new SKBitmap(cropRect.Width, cropRect.Height);
                using var croppedMask = new SKBitmap(cropRect.Width, cropRect.Height);
                
                using (var c = new SKCanvas(croppedImage))
                {
                    c.Clear(SKColors.Black);
                    // Draw source image offset by -cropRect.Left, -cropRect.Top
                    c.DrawBitmap(image, -cropRect.Left, -cropRect.Top);
                }
                using (var c = new SKCanvas(croppedMask))
                {
                    c.Clear(SKColors.Black);
                    c.DrawBitmap(mask, -cropRect.Left, -cropRect.Top);
                }
                
                // 4. Resize to 512x512 for Model
                using var resizedImage = croppedImage.Resize(new SKImageInfo(ModelInputSize, ModelInputSize, SKColorType.Rgba8888), SKSamplingOptions.Default);
                using var resizedMask = croppedMask.Resize(new SKImageInfo(ModelInputSize, ModelInputSize, SKColorType.Rgba8888), SKSamplingOptions.Default);

                if (resizedImage == null || resizedMask == null)
                {
                   throw new Exception("Failed to resize crop to 512x512");
                }
                
                // 5. Run Inference
                using var output512 = RunLaMaModel(resizedImage, resizedMask);
                
                // 6. Resize output back to Crop Size
                using var outputCroppedSize = output512.Resize(new SKImageInfo(cropRect.Width, cropRect.Height), SKSamplingOptions.Default);
                
                // 7. Composite back onto full image
                Console.WriteLine("[LaMaPatchService] Compositing patch back to original...");
                var result = image.Copy();
                
                using (var canvas = new SKCanvas(result))
                {
                    // Draw the patch at the crop position
                    canvas.DrawBitmap(outputCroppedSize, cropRect.Left, cropRect.Top);
                }
                
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

        private SKBitmap RunLaMaModel(SKBitmap resizedImage, SKBitmap resizedMask)
        {
            // Input checks
            if (resizedImage.Width != ModelInputSize || resizedImage.Height != ModelInputSize)
                throw new ArgumentException("RunLaMaModel expects 512x512 image");

            Console.WriteLine("[LaMaPatchService] Preparing tensors...");
            
            var inputPixelCount = ModelInputSize * ModelInputSize;
            var imageArray = new float[1 * 3 * inputPixelCount];
            var maskArray = new float[1 * 1 * inputPixelCount];
            
            // Offsets for channels
            int rOffset = 0;
            int gOffset = inputPixelCount;
            int bOffset = inputPixelCount * 2;

            // Iterate pixels
            for (int y = 0; y < ModelInputSize; y++)
            {
                for (int x = 0; x < ModelInputSize; x++)
                {
                    var pixel = resizedImage.GetPixel(x, y);
                    var maskPixel = resizedMask.GetPixel(x, y);
                    
                    var index = y * ModelInputSize + x;

                    // Normalize mask (single channel)
                    var maskVal = maskPixel.Red > 127 ? 1.0f : 0.0f;
                    maskArray[index] = maskVal; 

                    // Normalize image to [0, 1] AND mask it
                    var rVal = (pixel.Red / 255.0f) * (1.0f - maskVal);
                    var gVal = (pixel.Green / 255.0f) * (1.0f - maskVal);
                    var bVal = (pixel.Blue / 255.0f) * (1.0f - maskVal);

                    // RGB order
                    imageArray[rOffset + index] = rVal; 
                    imageArray[gOffset + index] = gVal;
                    imageArray[bOffset + index] = bVal;
                }
            }
            
            var imageTensor = new DenseTensor<float>(imageArray, new[] { 1, 3, ModelInputSize, ModelInputSize });
            var maskTensor = new DenseTensor<float>(maskArray, new[] { 1, 1, ModelInputSize, ModelInputSize });
            
            // Cleanup GC before inference
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Inputs
            var inputMeta = _session.InputMetadata;
            string imageInputName = inputMeta.Keys.FirstOrDefault(k => k.Contains("image") || k.Contains("input")) ?? "image";
            string maskInputName = inputMeta.Keys.FirstOrDefault(k => k.Contains("mask")) ?? "mask";

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(imageInputName, imageTensor),
                NamedOnnxValue.CreateFromTensor(maskInputName, maskTensor)
            };

            Console.WriteLine("[LaMaPatchService] Running inference...");
            using var results = _session.Run(inputs);
            var outputTensor = results.First().AsTensor<float>();

            // Convert to SKBitmap
            var outputBitmap = new SKBitmap(ModelInputSize, ModelInputSize, SKColorType.Rgba8888, SKAlphaType.Premul);
            
            float maxVal = 0f;
            maxVal = Math.Max(maxVal, outputTensor[0, 0, ModelInputSize / 2, ModelInputSize / 2]);
            bool isOutputZeroToOne = maxVal <= 1.0f;

            for (int y = 0; y < ModelInputSize; y++)
            {
                for (int x = 0; x < ModelInputSize; x++)
                {
                    float rVal = outputTensor[0, 0, y, x]; // R
                    float gVal = outputTensor[0, 1, y, x]; // G
                    float bVal = outputTensor[0, 2, y, x]; // B

                    if (isOutputZeroToOne)
                    {
                        rVal *= 255f;
                        gVal *= 255f;
                        bVal *= 255f;
                    }

                    var r = (byte)Math.Clamp(rVal, 0f, 255f);
                    var g = (byte)Math.Clamp(gVal, 0f, 255f);
                    var b = (byte)Math.Clamp(bVal, 0f, 255f);
                    
                    outputBitmap.SetPixel(x, y, new SKColor(r, g, b));
                }
            }
            return outputBitmap;
        }

        private SKRectI GetBoundingBox(SKBitmap mask)
        {
            int minX = mask.Width;
            int minY = mask.Height;
            int maxX = 0;
            int maxY = 0;
            bool found = false;

            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    var color = mask.GetPixel(x, y);
                    if (color.Red > 10 || color.Alpha > 10) 
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        found = true;
                    }
                }
            }
            
            if (!found) return SKRectI.Empty;
            return new SKRectI(minX, minY, maxX + 1, maxY + 1);
        }

        private SKRectI GetExpandedCropRect(SKRectI bbox, int imageWidth, int imageHeight)
        {
            // 1. Determine size. 
            // Must be at least the size of the bbox.
            // Target 3x context if possible, but don't strictly enforce if it makes the crop massive unless necessary.
            int requiredSize = Math.Max(bbox.Width, bbox.Height);
            int targetSize = requiredSize * 3;
            
            // 2. Constrain size - but NEVER smaller than the required bbox
            // (Original logic clamped to Min(W,H) which caused cutoffs for large masks)
            int maxSize = Math.Max(imageWidth, imageHeight); // Allow crop up to largest image dimension initially
            
            int size = targetSize;
            if (size > maxSize) size = maxSize;
            
            // If the mask itself is huge, we must process at least that size, even if it exceeds image bounds.
            if (size < requiredSize) size = requiredSize;

            // 3. Center
            int cx = bbox.MidX;
            int cy = bbox.MidY;
            
            int half = size / 2;
            int left = cx - half;
            int top = cy - half;
            
            // 4. Smart Clamp
            // Only slide the window if the size fits within the dimension.
            // If size > dimension, we center it (leave it negative/overflowing).
            
            if (size <= imageWidth)
            {
                if (left < 0) left = 0;
                if (left + size > imageWidth) left = imageWidth - size;
            }
            
            if (size <= imageHeight)
            {
                if (top < 0) top = 0;
                if (top + size > imageHeight) top = imageHeight - size;
            }
            
            return new SKRectI(left, top, left + size, top + size);
        }
    }
}
