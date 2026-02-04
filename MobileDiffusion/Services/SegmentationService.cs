using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using MobileDiffusion.Interfaces.Services;
using SkiaSharp;
using System.Diagnostics;

namespace MobileDiffusion.Services;

public class SegmentationService : ISegmentationService, IDisposable
{
    private readonly IImageService _imageService;

    private Stopwatch _stopwatch = new();
    private Task _initTask;
    private InferenceSession _encoderSession;
    private InferenceSession _decoderSession;

    private int _imageWidth;
    private int _imageHeight;
    private float _scaleX;
    private float _scaleY;
    private Tensor<float> _imageEmbeddings;
    private Tensor<float> _lowResMasks;

    public SKColor MaskColor => SKColors.Red;

    public SegmentationService(IImageService imageService)
    {
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));

        _ = InitAsync();
    }

    public void Dispose()
    {
        _encoderSession?.Dispose();
        _decoderSession?.Dispose();
    }

    public void Reset()
    {
        _lowResMasks = null;
    }

    public void UnloadModel()
    {
        Console.WriteLine("[SegmentationService] Unloading models to free memory...");
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
            GC.Collect();
        }
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

            // Set up encoder...
            var encoderPath = Path.Combine(FileSystem.CacheDirectory, "mobile_sam.encoder.onnx");
            if (!File.Exists(encoderPath))
            {
                Console.WriteLine("[SegmentationService] Extracting encoder to cache...");
                using var stream = await FileSystem.OpenAppPackageFileAsync("mobile_sam.encoder.onnx");
                using var fileStream = File.Create(encoderPath);
                await stream.CopyToAsync(fileStream);
            }
            _encoderSession = new InferenceSession(encoderPath, sessionOptions);

            // Set up decoder...
            var decoderPath = Path.Combine(FileSystem.CacheDirectory, "mobile_sam.decoder.onnx");
            if (!File.Exists(decoderPath))
            {
                Console.WriteLine("[SegmentationService] Extracting decoder to cache...");
                using var stream = await FileSystem.OpenAppPackageFileAsync("mobile_sam.decoder.onnx");
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

            var processedBitmap = PreprocessImage(bitmap);

            token.ThrowIfCancellationRequested();

            Console.WriteLine($"Resized image size: {processedBitmap.Width}x{processedBitmap.Height} pixels");

            // STEP - Create inputs for image encoder

            var imageDataTensor = ImageToDenseTensor(processedBitmap);

            token.ThrowIfCancellationRequested();

            var encoderInputs = new NamedOnnxValue[] { NamedOnnxValue.CreateFromTensor("input_image", imageDataTensor) };

            var encoderInputMeta = _encoderSession.InputMetadata;
            var encoderOutputMeta = _encoderSession.OutputMetadata;

            token.ThrowIfCancellationRequested();

            // STEP - Run image encoder

            _stopwatch.Restart();

            using (var encoderResult = _encoderSession.Run(encoderInputs))
            {
                _stopwatch.Stop();
                Console.WriteLine($"Encoder: {_stopwatch.Elapsed.TotalMilliseconds}ms");

                _imageEmbeddings = encoderResult.First().AsTensor<float>();
                _imageWidth = bitmap.Width;
                _imageHeight = bitmap.Height;
                _lowResMasks = null; // Reset mask state for new image
            }
        }
        catch (Exception)
        {
            _imageEmbeddings = null;
            _imageWidth = 0;
            _imageHeight = 0;
            _lowResMasks = null;
            return false;
        }
        finally
        {
            Console.WriteLine($"** SegmentationService.SetImage() FINISHED **");
        }

        return true;
    }

    public async Task<SKBitmap> DoSegmentation(SKPoint[] points, bool reset = false)
    {
        if (_imageEmbeddings == null ||
            _imageWidth == 0 ||
            _imageHeight == 0 ||
            points.Length == 0)
        {
            return null;
        }

        await InitAsync().ConfigureAwait(false);

        return await Task.Run(() =>
        {
            try
            {
                // STEP - Use image encoder output (image embeddings) as decoder input

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

                    labels.Add(i + 2);
                }

                // Add padding coordinate and batch index
                pointCoordsScaled.Add(0);
                pointCoordsScaled.Add(0);
                labels.Add(-1);

                var pointCoords = new DenseTensor<float>(pointCoordsScaled.ToArray(), new int[] { 1, pointCoordsScaled.Count / 2, 2 });
                var pointLabels = new DenseTensor<float>(labels.ToArray(), new int[] { 1, labels.Count });
                
                // Determine mask input
                Tensor<float> maskInput;
                Tensor<float> hasMask;

                if (!reset && _lowResMasks != null)
                {
                    maskInput = _lowResMasks;
                    hasMask = new DenseTensor<float>(new float[] { 1.0f }, new int[] { 1 });
                }
                else
                {
                    maskInput = new DenseTensor<float>(new float[256 * 256], new int[] { 1, 1, 256, 256 });
                    hasMask = new DenseTensor<float>(new float[] { 0.0f }, new int[] { 1 });
                }

                var originalImageSize = new DenseTensor<float>(new float[] { _imageHeight, _imageWidth }, new int[] { 2 });

                var decoderInputs = new NamedOnnxValue[]
                {
                    NamedOnnxValue.CreateFromTensor("image_embeddings", _imageEmbeddings),
                    NamedOnnxValue.CreateFromTensor("point_coords", pointCoords),
                    NamedOnnxValue.CreateFromTensor("point_labels", pointLabels),
                    NamedOnnxValue.CreateFromTensor("mask_input", maskInput),
                    NamedOnnxValue.CreateFromTensor("has_mask_input", hasMask),
                    NamedOnnxValue.CreateFromTensor("orig_im_size", originalImageSize)
                };

                // STEP -  Run decoder using inputs

                _stopwatch.Restart();

                using (var results = _decoderSession.Run(decoderInputs))
                {
                    _stopwatch.Stop();
                    Console.WriteLine($"Decoder: {_stopwatch.Elapsed.TotalMilliseconds}ms");

                    // Retrieve the output tensor(s)
                    var maskTensor = results.First(r => r.Name == "masks").AsTensor<float>();
                    var iouPredictionsTensor = results.First(r => r.Name == "iou_predictions").AsTensor<float>();
                    
                    // Cache low res masks for next iteration
                    var lowResMasksTensor = results.First(r => r.Name == "low_res_masks").AsTensor<float>();
                    _lowResMasks = new DenseTensor<float>(lowResMasksTensor.ToArray(), lowResMasksTensor.Dimensions);

                    return GetImageFromMaskTensorPointer(maskTensor);
                }
            }
            catch (Exception)
            {
                return null;
            }
        });   
    }

    private SKBitmap PreprocessImage(SKBitmap bitmap)
    {
        int targetWidth = 1024;
        int targetHeight = 1024;
        var resized = _imageService.GetResizedSKBitmap(bitmap, targetWidth, targetHeight, false, true, false);

        _scaleX = resized.Width / (float)bitmap.Width;
        _scaleY = resized.Height / (float)bitmap.Height;

        // Convert the color format if necessary
        resized = ConvertColorFormat(resized); // Convert to RGB if needed

        return resized;
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

        int width = image.Width;
        int height = image.Height;
        int channelCount = 3; // RGB channels
        int pixelCount = width * height;

        float[] rgbArray = new float[pixelCount * channelCount];

        // Get a pointer to the pixel data
        byte* pixels = (byte*)image.GetPixels().ToPointer();

        int pixelIndex = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte pixelR = pixels[pixelIndex++];
                byte pixelG = pixels[pixelIndex++];
                byte pixelB = pixels[pixelIndex++];
                pixelIndex++; // Skip the alpha channel
                
                int arrayIndex = (y * width + x) * channelCount;

                // Store the RGB values directly in the array
                rgbArray[arrayIndex] = pixelR;
                rgbArray[arrayIndex + 1] = pixelG;
                rgbArray[arrayIndex + 2] = pixelB;
            }
        }

        var imageDataTensor = new DenseTensor<float>(rgbArray, new int[] { image.Height, image.Width, 3 });

        _stopwatch.Stop();
        Console.WriteLine($"ImageToDenseTensor: {_stopwatch.Elapsed.TotalMilliseconds}ms");

        return imageDataTensor;
    }

    private SKBitmap GetImageFromMaskTensor(Tensor<float> tensor)
    {
        _stopwatch.Restart();

        var maskArray = tensor.ToArray();
        var pixelIndex = 0;
        var result = new SKBitmap(_imageWidth, _imageHeight, SKColorType.Rgba8888, SKAlphaType.Premul);

        for (var y = 0; y < _imageHeight; y++)
        {
            for (var x = 0; x < _imageWidth; x++)
            {
                var arrayValue = maskArray[pixelIndex++];

                var transformed = (byte)(arrayValue * 255f);

                if (transformed > 0.0)
                {
                    result.SetPixel(x, y, MaskColor);
                }
            }
        }

        _stopwatch.Stop();
        Console.WriteLine($"GetImageFromMaskTensor: {_stopwatch.Elapsed.TotalMilliseconds}ms");

        return result;
    }

    private unsafe SKBitmap GetImageFromMaskTensorPointer(Tensor<float> tensor)
    {
        _stopwatch.Restart();

        var maskArray = tensor.ToArray();
        var pixelIndex = 0;
        var result = new SKBitmap(_imageWidth, _imageHeight, SKColorType.Rgba8888, SKAlphaType.Premul);

        using (var pixmap = result.PeekPixels())
        {
            var pixels = (byte*)pixmap.GetPixels();
            var pixelStride = pixmap.Info.RowBytes;

            for (var y = 0; y < _imageHeight; y++)
            {
                for (var x = 0; x < _imageWidth; x++)
                {
                    var arrayValue = maskArray[pixelIndex++];
                    var transformed = (byte)(arrayValue * 255f);

                    if (transformed > 0)
                    {
                        var pixelPtr = pixels + y * pixelStride + x * 4; // Assuming 32-bit RGBA format
                        pixelPtr[0] = MaskColor.Red;    // Red
                        pixelPtr[1] = MaskColor.Green;  // Green
                        pixelPtr[2] = MaskColor.Blue;   // Blue
                        pixelPtr[3] = MaskColor.Alpha;  // Alpha
                    }
                }
            }
        }

        _stopwatch.Stop();
        Console.WriteLine($"GetImageFromMaskTensorPointer: {_stopwatch.Elapsed.TotalMilliseconds}ms");

        return result;
    }

}
