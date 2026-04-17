using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Mogri.Interfaces.Services;
using SkiaSharp;

namespace Mogri.Services
{
    public class AotGanPatchService : IPatchService, IDisposable
    {
        private InferenceSession? _session;
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
                var dataPath = Path.Combine(cacheDir, "aot_aot_model.data");

                // Ensure aot_aot_model.data is present
                if (!File.Exists(dataPath))
                {
                    Console.WriteLine("[AotGanPatchService] Extracting aot_model.data to cache...");
                    using var stream = await FileSystem.OpenAppPackageFileAsync("aot_model.data");
                    using var fileStream = File.Create(dataPath);
                    await stream.CopyToAsync(fileStream);
                    Console.WriteLine($"[AotGanPatchService] aot_model.data extracted to: {dataPath}");
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

                if (DeviceInfo.Current.DeviceType == DeviceType.Virtual)
                {
                    Console.WriteLine("[AotGanPatchService] Emulator detected: Using CPU settings");
                    // CRITICAL: Strict memory conservation for mobile devices
                    options.EnableCpuMemArena = false;
                    options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                    options.InterOpNumThreads = 2;
                    options.IntraOpNumThreads = 2;
                    options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_BASIC;

                    options.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_VERBOSE;
                }
                else
                {
                    Console.WriteLine("[AotGanPatchService] Physical device detected: Using NNAPI");
                    try
                    {
                        // Use NNAPI for physical devices
                        options.AppendExecutionProvider_Nnapi();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AotGanPatchService] Warning: Failed to append NNAPI provider: {ex.Message}");
                    }
                }

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

        public void Dispose()
        {
            Console.WriteLine("[AotGanPatchService] Disposing...");
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
            }
        }

        public void UnloadModel()
        {
            Console.WriteLine("[AotGanPatchService] Unloading model...");
            Dispose();
            GC.Collect();
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

        private unsafe SKBitmap RunAotGanModel(SKBitmap resizedImage, SKBitmap resizedMask)
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

            byte* imgPtr = (byte*)resizedImage.GetPixels().ToPointer();
            byte* maskPtr = (byte*)resizedMask.GetPixels().ToPointer();

            for (int i = 0; i < inputPixelCount; i++)
            {
                int ptrOffset = i * 4;

                // 1. Process Mask
                // AOT-GAN: 1 for invalid (masked) regions, 0 for valid.
                // FIX: Ensure we don't treat opaque black background as mask
                // Lower alpha threshold to 0 to catch faint mask strokes, but rely on Color check to exclude black background.
                // Rgba8888: R, G, B, A
                byte mr = maskPtr[ptrOffset];
                byte mg = maskPtr[ptrOffset + 1];
                byte mb = maskPtr[ptrOffset + 2];
                byte ma = maskPtr[ptrOffset + 3];

                bool isMask = ma > 0 && (mr > 10 || mg > 10 || mb > 10);
                var maskVal = isMask ? 1.0f : 0.0f;
                maskArray[i] = maskVal;

                // 2. Process Image
                byte r = imgPtr[ptrOffset];
                byte g = imgPtr[ptrOffset + 1];
                byte b = imgPtr[ptrOffset + 2];

                // Normalize to [0, 1]
                float rNorm = r / 255.0f;
                float gNorm = g / 255.0f;
                float bNorm = b / 255.0f;

                // 3. Apply Mask Logic
                if (maskVal > 0.5f)
                {
                    // Hole -> White (1.0)
                    imageArray[rOffset + i] = 1.0f;
                    imageArray[gOffset + i] = 1.0f;
                    imageArray[bOffset + i] = 1.0f;
                }
                else
                {
                    // Valid -> Normalized [0, 1] BGR pixel
                    imageArray[rOffset + i] = rNorm;
                    imageArray[gOffset + i] = gNorm;
                    imageArray[bOffset + i] = bNorm;
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

            if (_session == null)
            {
                throw new InvalidOperationException("Inference session not initialized");
            }

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
            byte* outPtr = (byte*)outputBitmap.GetPixels().ToPointer();

            bool isZeroOne = minVal >= 0 && maxVal <= 1.0f;

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
                    float rNorm, gNorm, bNorm;

                    if (isZeroOne)
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

                    int outIdx = (y * ModelInputSize + x) * 4;
                    outPtr[outIdx] = (byte)Math.Clamp(r, 0, 255);
                    outPtr[outIdx + 1] = (byte)Math.Clamp(g, 0, 255);
                    outPtr[outIdx + 2] = (byte)Math.Clamp(b, 0, 255);
                    outPtr[outIdx + 3] = 255; // Alpha
                }
            }

            return outputBitmap;
        }

        private unsafe SKRectI GetBoundingBox(SKBitmap mask)
        {
            int minX = mask.Width;
            int minY = mask.Height;
            int maxX = 0;
            int maxY = 0;
            bool found = false;

            int width = mask.Width;
            int height = mask.Height;
            byte* ptr = (byte*)mask.GetPixels().ToPointer();
            int rowBytes = mask.RowBytes;
            int bpp = mask.BytesPerPixel;

            // Optimization for standard 32-bit bitmaps (RGBA/BGRA)
            if (bpp == 4)
            {
                for (int y = 0; y < height; y++)
                {
                    byte* row = ptr + y * rowBytes;
                    for (int x = 0; x < width; x++)
                    {
                        // Assume Alpha is at index 3 (Standard for Rgba8888 and Bgra8888)
                        // Even if Argb8888 (Alpha at 0), this heuristic is decent if we check all channels.
                        // But explicitly: Skia standard is byte order R,G,B,A or B,G,R,A in memory.
                        // If we check b3 > 0 (Alpha) and any of b0,b1,b2 > 10 (Color)

                        // Note: If A is 0, then b3 is Blue or Red.
                        // However, standard Skia surfaces on mobile are Rgba8888 or Bgra8888.

                        byte b0 = row[x * 4];
                        byte b1 = row[x * 4 + 1];
                        byte b2 = row[x * 4 + 2];
                        byte b3 = row[x * 4 + 3];

                        if (b3 > 0 && (b0 > 10 || b1 > 10 || b2 > 10))
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                            found = true;
                        }
                    }
                }
            }
            else
            {
                // Fallback for other formats
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
            int targetSize = requiredSize * 2;

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

            var rect = new SKRectI(left, top, left + size, top + size);
            rect.Intersect(new SKRectI(0, 0, imageWidth, imageHeight));
            return rect;
        }

        private unsafe void EnsureMaskTransparency(SKBitmap mask)
        {
            // Fixes the issue where a black background is treated as "masked" by DstIn blending.
            // We force pixels that are visually black (background) to be fully transparent.

            // Direct pointer access avoids copying to/from SKBitmap.Pixels array
            if (mask.BytesPerPixel == 4)
            {
                byte* ptr = (byte*)mask.GetPixels().ToPointer();
                int width = mask.Width;
                int height = mask.Height;
                int rowBytes = mask.RowBytes;

                for (int y = 0; y < height; y++)
                {
                    byte* row = ptr + y * rowBytes;
                    for (int x = 0; x < width; x++)
                    {
                        // Check if black but has alpha
                        // Assume Alpha at index 3 for 32-bit SKBitmap
                        int offset = x * 4;
                        byte b0 = row[offset];
                        byte b1 = row[offset + 1];
                        byte b2 = row[offset + 2];
                        byte b3 = row[offset + 3];

                        if (b3 > 0 && b0 < 10 && b1 < 10 && b2 < 10)
                        {
                            // Set to transparent (all 0)
                            // Casting to int* to write 4 bytes at once
                            *(int*)(row + offset) = 0;
                        }
                    }
                }
            }
            else
            {
                // Fallback for non-standard formats
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
}