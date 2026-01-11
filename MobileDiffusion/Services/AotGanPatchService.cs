using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using MobileDiffusion.Interfaces.Services;
using SkiaSharp;

namespace MobileDiffusion.Services
{
    public class AotGanPatchService : IPatchService
    {
        private InferenceSession _session;
        private const int ModelInputSize = 512;
        private bool _isLoading = false;

        public AotGanPatchService()
        {
        }

        private async Task InitializeModelAsync()
        {
            if (_session != null || _isLoading) return;
            _isLoading = true;
            Console.WriteLine("[AotGanPatchService] InitializeModelAsync started.");

            try
            {
                var cacheDir = FileSystem.CacheDirectory;
                var modelPath = Path.Combine(cacheDir, "aot_gan.onnx");
                var dataPath = Path.Combine(cacheDir, "model.data");

                // Ensure model.data is present
                if (!File.Exists(dataPath))
                {
                    Console.WriteLine("[AotGanPatchService] Extracting model.data to cache...");
                    using var stream = await FileSystem.OpenAppPackageFileAsync("model.data");
                    using var fileStream = File.Create(dataPath);
                    await stream.CopyToAsync(fileStream);
                    Console.WriteLine($"[AotGanPatchService] model.data extracted to: {dataPath}");
                }

                // Ensure main model is present
                if (!File.Exists(modelPath))
                {
                    Console.WriteLine("[AotGanPatchService] Extracting aot_gan.onnx to cache...");
                    using var stream = await FileSystem.OpenAppPackageFileAsync("aot_gan.onnx");
                    using var fileStream = File.Create(modelPath);
                    await stream.CopyToAsync(fileStream);
                     Console.WriteLine($"[AotGanPatchService] Model extracted to: {modelPath}");
                }
                
                var options = new SessionOptions();
                
                // CRITICAL: Strict memory conservation for mobile devices
                options.EnableCpuMemArena = false;
                options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                options.InterOpNumThreads = 2;
                options.IntraOpNumThreads = 2;
                options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_BASIC;
                
                options.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_VERBOSE;

                _session = new InferenceSession(modelPath, options);
                Console.WriteLine("[AotGanPatchService] InferenceSession created successfully.");
                
                // Log metadata
                foreach (var input in _session.InputMetadata)
                {
                    Console.WriteLine($"[AotGanPatchService] Input: {input.Key}, Type: {input.Value.ElementType}, dimensions: {string.Join(",", input.Value.Dimensions)}");
                }
                foreach (var output in _session.OutputMetadata)
                {
                    Console.WriteLine($"[AotGanPatchService] Output: {output.Key}, Type: {output.Value.ElementType}, dimensions: {string.Join(",", output.Value.Dimensions)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AotGanPatchService] Failed to load AOT-GAN model: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                _isLoading = false;
            }
        }

        public void UnloadModel()
        {
            Console.WriteLine("[AotGanPatchService] Unloading model...");
            try
            {
                _session?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AotGanPatchService] Error disposing session: {ex}");
            }
            finally
            {
                _session = null;
                GC.Collect();
            }
        }

        public async Task<SKBitmap> PatchImageAsync(SKBitmap image, SKBitmap mask)
        {
            Console.WriteLine("[AotGanPatchService] PatchImageAsync started");
            try
            {
                if (_session == null)
                {
                    await InitializeModelAsync();
                    if (_session == null) 
                    {
                        Console.WriteLine("[AotGanPatchService] Failed to initialize session");
                        throw new InvalidOperationException("Model not loaded");
                    }
                }

                return await Task.Run(() => 
                {
                    // 1. Get Bounding Box since 4K resizing is destructive
                    Console.WriteLine("[AotGanPatchService] Calculating ROI...");
                    var bbox = GetBoundingBox(mask);
                    if (bbox.IsEmpty)
                    {
                        Console.WriteLine("[AotGanPatchService] Empty mask, returning original image.");
                        return image.Copy();
                    }
                    
                    // 2. Expand to Square Crop
                    var cropRect = GetExpandedCropRect(bbox, image.Width, image.Height);
                    Console.WriteLine($"[AotGanPatchService] ROI: {bbox}, Crop: {cropRect}");
                    
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
                        c.Clear(SKColors.Transparent);
                        c.DrawBitmap(mask, -cropRect.Left, -cropRect.Top);
                    }

                    // FIX: Ensure mask is truly transparent where black/empty, to support DstIn blending.
                    EnsureMaskTransparency(croppedMask);
                    
                    // 4. Resize to 512x512 for Model
                    using var resizedImage = croppedImage.Resize(new SKImageInfo(ModelInputSize, ModelInputSize, SKColorType.Rgba8888), SKSamplingOptions.Default);
                    // Use Nearest Neighbor for mask to preserve hard edges and avoid gray halos
                    using var resizedMask = croppedMask.Resize(new SKImageInfo(ModelInputSize, ModelInputSize, SKColorType.Rgba8888), new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));

                    if (resizedImage == null || resizedMask == null)
                    {
                    throw new Exception("Failed to resize crop to 512x512");
                    }
                    
                    // 5. Run Inference
                    using var output512 = RunAotGanModel(resizedImage, resizedMask);
                    
                    // 6. Resize output back to Crop Size
                    using var outputCroppedSize = output512.Resize(new SKImageInfo(cropRect.Width, cropRect.Height), SKSamplingOptions.Default);
                    
                    // 7. Composite back onto full image
                    Console.WriteLine("[AotGanPatchService] Compositing patch back to original...");
                    var result = image.Copy();
                    
                    using (var canvas = new SKCanvas(result))
                    {
                        // Use a layer to mask the prediction
                        // We want to draw 'outputCroppedSize' but only where 'croppedMask' indicates a hole.
                        
                        using (var paint = new SKPaint())
                        {
                            // 1. Save a layer for the crop area
                            canvas.SaveLayer(new SKRect(cropRect.Left, cropRect.Top, cropRect.Right, cropRect.Bottom), null);
                            
                            // 2. Draw the predicted patch (opaque)
                            canvas.DrawBitmap(outputCroppedSize, cropRect.Left, cropRect.Top);
                            
                            // 3. Mask it with the original mask using DstIn (Keep DST (prediction) where SRC (mask) is opaque)
                            // The croppedMask likely has high alpha where the user painted.
                            paint.BlendMode = SKBlendMode.DstIn;
                            canvas.DrawBitmap(croppedMask, cropRect.Left, cropRect.Top, paint);
                            
                            // 4. Restore the layer, which composites the masked prediction onto the original image
                            canvas.Restore();
                        }
                    }
                    
                    Console.WriteLine("[AotGanPatchService] PatchImageAsync completed successfully");
                    return result;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AotGanPatchService] ERROR in PatchImageAsync: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw;
            }
        }

        private SKBitmap RunAotGanModel(SKBitmap resizedImage, SKBitmap resizedMask)
        {
            if (resizedImage.Width != ModelInputSize || resizedImage.Height != ModelInputSize)
                throw new ArgumentException($"RunAotGanModel expects {ModelInputSize}x{ModelInputSize} image");

            Console.WriteLine("[AotGanPatchService] Preparing tensors for AOT-GAN...");
            
            var inputPixelCount = ModelInputSize * ModelInputSize;
            var imageArray = new float[1 * 3 * inputPixelCount];
            var maskArray = new float[1 * 1 * inputPixelCount];
            
            // Channel offsets
            // Note: AOT-GAN (and many PyTorch conversions referenced in InpaintMask.cs) expect BGR layout.
            // BGR: Channel 0 = Blue, Channel 1 = Green, Channel 2 = Red.
            int bOffset = 0;
            int gOffset = inputPixelCount;
            int rOffset = inputPixelCount * 2;

            for (int y = 0; y < ModelInputSize; y++)
            {
                for (int x = 0; x < ModelInputSize; x++)
                {
                    var pixel = resizedImage.GetPixel(x, y);
                    var maskPixel = resizedMask.GetPixel(x, y);
                    
                    var index = y * ModelInputSize + x;

                    // 1. Process Mask
                    // AOT-GAN: 1 for invalid (masked) regions, 0 for valid.
                    // FIX: Ensure we don't treat opaque black background as mask
                    // Lower alpha threshold to 0 to catch faint mask strokes, but rely on Color check to exclude black background.
                    bool isMask = maskPixel.Alpha > 0 && (maskPixel.Red > 10 || maskPixel.Green > 10 || maskPixel.Blue > 10);
                    var maskVal = isMask ? 1.0f : 0.0f;
                    maskArray[index] = maskVal;

                    // 2. Process Image
                    // Based on empirical testing (variation 3_BGR_0_1_Fill1.png),
                    // the model performs best with:
                    // - BGR Input
                    // - [0, 1] Normalization (NOT [-1, 1])
                    // - White (1.0) Hole Filling

                    // Normalize to [0, 1]
                    float rNorm = pixel.Red / 255.0f;
                    float gNorm = pixel.Green / 255.0f;
                    float bNorm = pixel.Blue / 255.0f;
                    
                    // 3. Apply Mask Logic
                    // image_masked = (image * (1 - mask)) + mask
                    // Hole -> 1.0
                    
                    if (maskVal > 0.5f)
                    {
                        // Hole -> White (1.0)
                        imageArray[rOffset + index] = 1.0f;
                        imageArray[gOffset + index] = 1.0f;
                        imageArray[bOffset + index] = 1.0f;
                    }
                    else
                    {
                        // Valid -> Normalized [0, 1] BGR pixel
                        // Note: rOffset/gOffset/bOffset logic is already set for BGR layout above
                        
                        imageArray[rOffset + index] = rNorm;
                        imageArray[gOffset + index] = gNorm;
                        imageArray[bOffset + index] = bNorm;
                    }
                }
            }
            
            var imageTensor = new DenseTensor<float>(imageArray, new[] { 1, 3, ModelInputSize, ModelInputSize });
            var maskTensor = new DenseTensor<float>(maskArray, new[] { 1, 1, ModelInputSize, ModelInputSize });
            
            // Inputs
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("image", imageTensor),
                NamedOnnxValue.CreateFromTensor("mask", maskTensor)
            };

            Console.WriteLine("[AotGanPatchService] Running inference...");
            
            // Force GC before run to clear previous large buffers
            GC.Collect();
            
            using var results = _session.Run(inputs);
            var outputTensor = results.First().AsTensor<float>();

            // Analyze output range to adaptively denormalize
            float minVal = float.MaxValue;
            float maxVal = float.MinValue;
            foreach (var val in outputTensor)
            {
                if (val < minVal) minVal = val;
                if (val > maxVal) maxVal = val;
            }

            // Post-process
            var outputBitmap = new SKBitmap(ModelInputSize, ModelInputSize, SKColorType.Rgba8888, SKAlphaType.Premul);
            
            for (int y = 0; y < ModelInputSize; y++)
            {
                for (int x = 0; x < ModelInputSize; x++)
                {
                    // layout is 1, 3, H, W.
                    // Since we used BGR layout for input, the model likely returns BGR.
                    // So Channel 0 = Blue, Channel 1 = Green, Channel 2 = Red.
                    float bRaw = outputTensor[0, 0, y, x];
                    float gRaw = outputTensor[0, 1, y, x];
                    float rRaw = outputTensor[0, 2, y, x];
                    
                    // Denormalize
                    // If range is approx [-1, 1]: (val + 1) / 2 * 255
                    // If range is approx [0, 1]: val * 255
                    
                    float rNorm, gNorm, bNorm;
                    
                    if (minVal >= 0 && maxVal <= 1.0f)
                    {
                         // Likely [0, 1] range
                         rNorm = rRaw;
                         gNorm = gRaw;
                         bNorm = bRaw;
                    }
                    else
                    {
                        // Assume [-1, 1] range
                        rNorm = rRaw * 0.5f + 0.5f;
                        gNorm = gRaw * 0.5f + 0.5f;
                        bNorm = bRaw * 0.5f + 0.5f;
                    }

                    int r = (int)(rNorm * 255.0f);
                    int g = (int)(gNorm * 255.0f);
                    int b = (int)(bNorm * 255.0f);
                    
                    outputBitmap.SetPixel(x, y, new SKColor((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255)));
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
                    // FIX: Ensure we don't treat opaque black background as mask
                    if (color.Alpha > 0 && (color.Red > 10 || color.Green > 10 || color.Blue > 10)) 
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

            // AOT-GAN works best with significant context. 1.25x is often too tight, causing "blob" artifacts.
            // 3x provides a good balance of context vs resolution.
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

        private void EnsureMaskTransparency(SKBitmap mask)
        {
            // Fixes the issue where a black background is treated as "masked" by DstIn blending.
            // We force pixels that are visually black (background) to be fully transparent.
            
            var pixels = mask.Pixels;
            bool modified = false;

            for (int i = 0; i < pixels.Length; i++)
            {
                // If it has alpha but no color content, treated as transparent background
                if (pixels[i].Alpha > 0 && pixels[i].Red < 10 && pixels[i].Green < 10 && pixels[i].Blue < 10)
                {
                    pixels[i] = SKColors.Transparent;
                    modified = true;
                }
            }

            if (modified)
            {
                mask.Pixels = pixels;
            }
        }
    }
}
