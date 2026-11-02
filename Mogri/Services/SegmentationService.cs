using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Mogri.Interfaces.Services;
using SkiaSharp;
using System.Diagnostics;

namespace Mogri.Services;

public class SegmentationService : ISegmentationService, IDisposable
{
    private readonly IImageService _imageService;

    private Stopwatch _stopwatch = new();
    private Task? _initTask;
    private InferenceSession? _encoderSession;
    private InferenceSession? _decoderSession;

    private int _imageWidth;
    private int _imageHeight;
    private int _resizedWidth;
    private int _resizedHeight;
    private float _scaleX;
    private float _scaleY;
    private Tensor<float>? _imageEmbeddings;

    public SKColor MaskColor => SKColors.Red;

    public SegmentationService(IImageService imageService)
    {
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));

        _ = InitAsync();
    }

    public void Dispose()
    {
        Console.WriteLine("[SegmentationService] Disposing...");
        try
        {
            _encoderSession?.Dispose();
            _decoderSession?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SegmentationService] Error disposing sessions: {ex}");
        }
        finally
        {
            _encoderSession = null;
            _decoderSession = null;
            _initTask = null;
        }
    }

    public void Reset()
    {
    }

    public SKBitmap InvertMask(SKBitmap currentMask)
    {
        var result = new SKBitmap(currentMask.Info);
        var maskColor = MaskColor;
        
        unsafe
        {
            var srcPtr = (byte*)currentMask.GetPixels().ToPointer();
            var dstPtr = (byte*)result.GetPixels().ToPointer();
            int length = currentMask.Width * currentMask.Height * 4;
            
            for (int i = 0; i < length; i += 4)
            {
                // Check alpha channel (index 3)
                if (srcPtr[i + 3] == 0)
                {
                    dstPtr[i] = maskColor.Red;
                    dstPtr[i + 1] = maskColor.Green;
                    dstPtr[i + 2] = maskColor.Blue;
                    dstPtr[i + 3] = maskColor.Alpha;
                }
                else
                {
                    dstPtr[i] = 0;
                    dstPtr[i + 1] = 0;
                    dstPtr[i + 2] = 0;
                    dstPtr[i + 3] = 0;
                }
            }
        }
        
        return result;
    }

    public void UnloadModel()
    {
        Console.WriteLine("[SegmentationService] Unloading models to free memory...");
        Dispose();
        GC.Collect();
    }

    Task InitAsync()
    {
        if (_initTask == null || _initTask.IsFaulted)
            _initTask = Task.Run(InitTask);

        return _initTask;
    }

    async Task InitTask()
    {
        try
        {
            var sessionOptions = new SessionOptions();

#if ANDROID
            sessionOptions.AppendExecutionProvider_Nnapi();
#elif IOS
            sessionOptions.AppendExecutionProvider_CoreML();
#endif

            // Clean up legacy MobileSAM cached models if present
            cleanUpLegacyModels();

            // Set up encoder...
            var encoderPath = Path.Combine(FileSystem.CacheDirectory, "edge_sam_3x_encoder.onnx");
            if (!File.Exists(encoderPath))
            {
                Console.WriteLine("[SegmentationService] Extracting encoder to cache...");
                using var stream = await FileSystem.OpenAppPackageFileAsync("edge_sam_3x_encoder.onnx");
                using var fileStream = File.Create(encoderPath);
                await stream.CopyToAsync(fileStream);
            }
            _encoderSession = new InferenceSession(encoderPath, sessionOptions);

            // Set up decoder...
            var decoderPath = Path.Combine(FileSystem.CacheDirectory, "edge_sam_3x_decoder.onnx");
            if (!File.Exists(decoderPath))
            {
                Console.WriteLine("[SegmentationService] Extracting decoder to cache...");
                using var stream = await FileSystem.OpenAppPackageFileAsync("edge_sam_3x_decoder.onnx");
                using var fileStream = File.Create(decoderPath);
                await stream.CopyToAsync(fileStream);
            }
            _decoderSession = new InferenceSession(decoderPath, sessionOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error initializing SegmentationService: {ex}");
            // Consider rethrowing or handling appropriately depending on app requirements
        }
    }

    private static void cleanUpLegacyModels()
    {
        try
        {
            var legacyEncoder = Path.Combine(FileSystem.CacheDirectory, "mobile_sam.encoder.onnx");
            if (File.Exists(legacyEncoder))
            {
                File.Delete(legacyEncoder);
                Console.WriteLine("[SegmentationService] Cleaned up legacy mobile_sam.encoder.onnx from cache.");
            }

            var legacyDecoder = Path.Combine(FileSystem.CacheDirectory, "mobile_sam.decoder.onnx");
            if (File.Exists(legacyDecoder))
            {
                File.Delete(legacyDecoder);
                Console.WriteLine("[SegmentationService] Cleaned up legacy mobile_sam.decoder.onnx from cache.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SegmentationService] Could not clean up legacy cached models: {ex.Message}");
        }
    }

    public async Task<bool> SetImage(SKBitmap bitmap, CancellationToken token)
    {
        if (bitmap == null)
        {
            return false;
        }

        await InitAsync().ConfigureAwait(false);

        try
        {
            Console.WriteLine($"** SegmentationService.SetImage() STARTED **");

            token.ThrowIfCancellationRequested();

            // STEP - Preprocess SKBitmap image so it matches expected color format/dimensions

            Console.WriteLine($"Original image size: {bitmap.Width}x{bitmap.Height} pixels");

            // Keep derived values local until the request survives the final cancellation check.
            var (processedBitmap, scaleX, scaleY) = PreprocessImage(bitmap);

            token.ThrowIfCancellationRequested();

            Console.WriteLine($"Resized image size: {processedBitmap.Width}x{processedBitmap.Height} pixels");

            // STEP - Create inputs for image encoder

            var imageDataTensor = ImageToDenseTensor(processedBitmap);

            token.ThrowIfCancellationRequested();

            var encoderInputs = new NamedOnnxValue[] { NamedOnnxValue.CreateFromTensor("image", imageDataTensor) };

            if (_encoderSession == null)
            {
                Console.WriteLine("Encoder session is null.");
                return false;
            }

            var encoderInputMeta = _encoderSession.InputMetadata;
            var encoderOutputMeta = _encoderSession.OutputMetadata;

            token.ThrowIfCancellationRequested();

            // STEP - Run image encoder

            _stopwatch.Restart();

            using (var encoderResult = _encoderSession.Run(encoderInputs))
            {
                _stopwatch.Stop();
                Console.WriteLine($"Encoder: {_stopwatch.Elapsed.TotalMilliseconds}ms");

                // Run is synchronous, so this is the first point where a canceled older request can bail out
                // without overwriting embeddings for a newer image.
                token.ThrowIfCancellationRequested();

                _imageEmbeddings = encoderResult.First().AsTensor<float>();
                _imageWidth = bitmap.Width;
                _imageHeight = bitmap.Height;
                _resizedWidth = processedBitmap.Width;
                _resizedHeight = processedBitmap.Height;
                _scaleX = scaleX;
                _scaleY = scaleY;
            }
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            _imageEmbeddings = null;
            _imageWidth = 0;
            _imageHeight = 0;
            _resizedWidth = 0;
            _resizedHeight = 0;
            _scaleX = 0;
            _scaleY = 0;
            return false;
        }
        finally
        {
            Console.WriteLine($"** SegmentationService.SetImage() FINISHED **");
        }

        return true;
    }

    public async Task<SKBitmap?> DoSegmentation(SKPoint[] points, bool reset = false)
    {
        if (_imageEmbeddings == null ||
            _imageWidth == 0 ||
            _imageHeight == 0 ||
            points.Length == 0)
        {
            return null;
        }

        await InitAsync().ConfigureAwait(false);

        if (_decoderSession == null)
        {
            return null;
        }

        return await Task.Run(() =>
        {
            try
            {
                // STEP - Use image encoder output (image embeddings) as decoder input
                if (_decoderSession == null) return null;

                var decoderInputMeta = _decoderSession.InputMetadata;
                var decoderOutputMeta = _decoderSession.OutputMetadata;

                // Transform tap coordinates based on scaled input image
                var pointCoordsScaled = new List<float>();
                var labels = new List<float>();

                for (int i = 0; i < points.Length; i++)
                {
                    var point = points[i];
                    pointCoordsScaled.Add(point.X * _scaleX);
                    pointCoordsScaled.Add(point.Y * _scaleY);

                    if (points.Length == 2)
                    {
                        // 2 points represents a gesture bounding box [topLeft, bottomRight]
                        // 2.0 = box top-left, 3.0 = box bottom-right
                        labels.Add(i + 2);
                    }
                    else
                    {
                        // Single tap or discrete point click(s)
                        // 1.0 = positive/foreground click
                        labels.Add(1.0f);
                    }
                }

                var pointCoords = new DenseTensor<float>(pointCoordsScaled.ToArray(), new int[] { 1, points.Length, 2 });
                var pointLabels = new DenseTensor<float>(labels.ToArray(), new int[] { 1, points.Length });

                var decoderInputs = new NamedOnnxValue[]
                {
                    NamedOnnxValue.CreateFromTensor("image_embeddings", _imageEmbeddings),
                    NamedOnnxValue.CreateFromTensor("point_coords", pointCoords),
                    NamedOnnxValue.CreateFromTensor("point_labels", pointLabels)
                };

                // STEP -  Run decoder using inputs

                _stopwatch.Restart();

                using (var results = _decoderSession.Run(decoderInputs))
                {
                    _stopwatch.Stop();
                    Console.WriteLine($"Decoder: {_stopwatch.Elapsed.TotalMilliseconds}ms");

                    var scoresTensor = results.First(r => r.Name == "scores").AsTensor<float>();
                    var maskTensor = results.First(r => r.Name == "masks").AsTensor<float>();

                    int bestIndex = 0;
                    float bestScore = float.NegativeInfinity;
                    int numMasks = scoresTensor.Dimensions[1];
                    for (int i = 0; i < numMasks; i++)
                    {
                        float score = scoresTensor[0, i];
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestIndex = i;
                        }
                    }

                    return GetImageFromMaskTensor(maskTensor, bestIndex);
                }
            }
            catch (Exception)
            {
                return null;
            }
        });
    }

    private (SKBitmap ProcessedBitmap, float ScaleX, float ScaleY) PreprocessImage(SKBitmap bitmap)
    {
        int targetWidth = 1024;
        int targetHeight = 1024;
        var resized = _imageService.GetResizedSKBitmap(bitmap, targetWidth, targetHeight, false, true, false);

        if (resized == null)
        {
            throw new Exception("Failed to resize image");
        }

        var scaleX = resized.Width / (float)bitmap.Width;
        var scaleY = resized.Height / (float)bitmap.Height;

        // Convert the color format if necessary
        resized = ConvertColorFormat(resized); // Convert to RGB if needed

        return (resized, scaleX, scaleY);
    }

    private SKBitmap ConvertColorFormat(SKBitmap image)
    {
        const SKColorType targetColorType = SKColorType.Rgba8888;

        var colorType = image.ColorType;

        if (colorType == targetColorType)
        {
            return image;
        }

        // Create a new SKBitmap with the specified color type
        var convertedImage = new SKBitmap(image.Width, image.Height, targetColorType, image.AlphaType);

        // Copy the pixels from the original image to the converted image
        image.CopyTo(convertedImage, targetColorType);

        return convertedImage;
    }

    private unsafe DenseTensor<float> ImageToDenseTensor(SKBitmap image)
    {
        _stopwatch.Restart();

        const int targetSize = 1024;
        const int channelSize = targetSize * targetSize;
        int width = image.Width;
        int height = image.Height;

        // ImageNet normalization constants expected by EdgeSAM
        const float meanR = 123.675f;
        const float meanG = 116.28f;
        const float meanB = 103.53f;
        const float stdR = 58.395f;
        const float stdG = 57.12f;
        const float stdB = 57.375f;

        // Allocate 1x3x1024x1024 array (initialized to 0.0f for padding)
        float[] chwArray = new float[3 * channelSize];

        byte* pixels = (byte*)image.GetPixels().ToPointer();
        int pixelStride = image.RowBytes;

        for (int y = 0; y < height; y++)
        {
            byte* rowPtr = pixels + y * pixelStride;
            int rowOffset = y * targetSize;

            for (int x = 0; x < width; x++)
            {
                int pixelOffset = rowOffset + x;
                byte pixelR = rowPtr[x * 4];
                byte pixelG = rowPtr[x * 4 + 1];
                byte pixelB = rowPtr[x * 4 + 2];

                chwArray[0 * channelSize + pixelOffset] = (pixelR - meanR) / stdR;
                chwArray[1 * channelSize + pixelOffset] = (pixelG - meanG) / stdG;
                chwArray[2 * channelSize + pixelOffset] = (pixelB - meanB) / stdB;
            }
        }

        var imageDataTensor = new DenseTensor<float>(chwArray, new int[] { 1, 3, targetSize, targetSize });

        _stopwatch.Stop();
        Console.WriteLine($"ImageToDenseTensor: {_stopwatch.Elapsed.TotalMilliseconds}ms");

        return imageDataTensor;
    }

    private unsafe SKBitmap GetImageFromMaskTensor(Tensor<float> tensor, int maskIndex)
    {
        _stopwatch.Restart();

        const int lowResSize = 256;
        var maskArray = tensor.ToArray();
        int maskOffset = maskIndex * lowResSize * lowResSize;

        // Create 256x256 Alpha8 bitmap containing the sigmoid probability map
        using var lowResBitmap = new SKBitmap(lowResSize, lowResSize, SKColorType.Alpha8, SKAlphaType.Premul);
        byte* lowResPixels = (byte*)lowResBitmap.GetPixels().ToPointer();

        for (int i = 0; i < lowResSize * lowResSize; i++)
        {
            float logit = maskArray[maskOffset + i];
            float prob = 1.0f / (1.0f + MathF.Exp(-logit));
            lowResPixels[i] = (byte)(prob * 255.0f);
        }

        // Upscale the valid (unpadded) region of the low-res mask back to original image dimensions
        using var upscaledAlpha = new SKBitmap(_imageWidth, _imageHeight, SKColorType.Alpha8, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(upscaledAlpha))
        {
            canvas.Clear(SKColors.Transparent);
            var srcRect = new SKRect(0, 0, (float)_resizedWidth / 4.0f, (float)_resizedHeight / 4.0f);
            var dstRect = new SKRect(0, 0, _imageWidth, _imageHeight);
            canvas.DrawBitmap(lowResBitmap, srcRect, dstRect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        }

        var result = new SKBitmap(_imageWidth, _imageHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        var maskColor = MaskColor;

        using (var pixmap = result.PeekPixels())
        {
            byte* dstPixels = (byte*)pixmap.GetPixels().ToPointer();
            int dstStride = pixmap.Info.RowBytes;
            byte* alphaPixels = (byte*)upscaledAlpha.GetPixels().ToPointer();
            int alphaStride = upscaledAlpha.RowBytes;

            for (var y = 0; y < _imageHeight; y++)
            {
                byte* alphaRow = alphaPixels + y * alphaStride;
                byte* dstRow = dstPixels + y * dstStride;

                for (var x = 0; x < _imageWidth; x++)
                {
                    int pixelOffset = x * 4;
                    if (alphaRow[x] >= 128)
                    {
                        dstRow[pixelOffset] = maskColor.Red;
                        dstRow[pixelOffset + 1] = maskColor.Green;
                        dstRow[pixelOffset + 2] = maskColor.Blue;
                        dstRow[pixelOffset + 3] = maskColor.Alpha;
                    }
                    else
                    {
                        dstRow[pixelOffset] = 0;
                        dstRow[pixelOffset + 1] = 0;
                        dstRow[pixelOffset + 2] = 0;
                        dstRow[pixelOffset + 3] = 0;
                    }
                }
            }
        }

        _stopwatch.Stop();
        Console.WriteLine($"GetImageFromMaskTensor: {_stopwatch.Elapsed.TotalMilliseconds}ms");

        return result;
    }

}