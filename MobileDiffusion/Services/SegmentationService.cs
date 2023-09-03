using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using MobileDiffusion.Interfaces.Services;
using SkiaSharp;
using System.Diagnostics;

namespace MobileDiffusion.Services;

public class SegmentationService : ISegmentationService
{
    private readonly IImageService _imageService;

    private static readonly float[] meanArray = new float[] { 123.675f, 116.28f, 103.53f };
    private static readonly float[] stdArray = new float[] { 58.395f, 57.12f, 57.375f };
    private readonly DenseTensor<float> meanTensor = new(meanArray, new int[] { 1, 3, 1, 1 });
    private readonly DenseTensor<float> stdTensor = new(stdArray, new int[] { 1, 3, 1, 1 });
    private const int imgSize = 224;

    private Task _initTask;
    private byte[] _encoderModel;
    private byte[] _decoderModel;
    private InferenceSession _encoderSession;
    private InferenceSession _decoderSession;

    private int _imageWidth;
    private int _imageHeight;
    private Tensor<float> _imageEmbeddings;

    public SegmentationService(IImageService imageService)
    {
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));

        _ = InitAsync();
    }

    Task InitAsync()
    {
        if (_initTask == null || _initTask.IsFaulted)
            _initTask = Task.Run(InitTask);

        return _initTask;
    }

    async Task InitTask()
    {
        var sessionOptions = new SessionOptions();

#if ANDROID
        sessionOptions.AppendExecutionProvider_Nnapi();
#elif IOS
        sessionOptions.AppendExecutionProvider_CoreML();
#endif

        // Set up encoder...

        using var encoderFileStream = await FileSystem.OpenAppPackageFileAsync("mobile_sam.encoder.onnx");
        using var encoderMemoryStream = new MemoryStream();
        await encoderFileStream.CopyToAsync(encoderMemoryStream);

        _encoderModel = encoderMemoryStream.ToArray();
        _encoderSession = new InferenceSession(_encoderModel, sessionOptions);

        // Set up decoder...

        using var decoderFileStream = await FileSystem.OpenAppPackageFileAsync("mobile_sam.decoder.onnx");
        using var decoderMemoryStream = new MemoryStream();
        await decoderFileStream.CopyToAsync(decoderMemoryStream);

        _decoderModel = decoderMemoryStream.ToArray();
        _decoderSession = new InferenceSession(_decoderModel, sessionOptions);
    }

    public async Task<bool> SetImage(SKBitmap bitmap, CancellationToken token)
    {
        if (bitmap == null)
        {
            return false;
        }

        await _initTask.ConfigureAwait(false);

        try
        {
            Console.WriteLine($"** SegmentationService.SetImage() STARTED **");

            token.ThrowIfCancellationRequested();

            // STEP - Preprocess SKBitmap image so it matches expected color format/dimensions

            Console.WriteLine($"Provided image size: {bitmap.Width}x{bitmap.Height} pixels");

            var processedBitmap = PreprocessImage(bitmap);

            token.ThrowIfCancellationRequested();

            Console.WriteLine($"Resized image size: {processedBitmap.Width}x{processedBitmap.Height} pixels");

            // STEP - Create inputs for image encoder

            var imageDataTensor = ImageToDenseTensor(processedBitmap);

            token.ThrowIfCancellationRequested();

            //imageDataTensor = NormalizeAndPadImageTensor(imageDataTensor);

            token.ThrowIfCancellationRequested();

            var encoderInputs = new NamedOnnxValue[] { NamedOnnxValue.CreateFromTensor("input_image", imageDataTensor) };

            var encoderInputMeta = _encoderSession.InputMetadata;
            var encoderOutputMeta = _encoderSession.OutputMetadata;

            token.ThrowIfCancellationRequested();

            // STEP - Run image encoder

            using (var encoderResult = _encoderSession.Run(encoderInputs))
            {
                _imageEmbeddings = encoderResult.First().AsTensor<float>();
                _imageWidth = bitmap.Width;
                _imageHeight = bitmap.Height;
            }
        }
        catch (Exception ex)
        {
            _imageEmbeddings = null;
            _imageWidth = 0;
            _imageHeight = 0;
            return false;
        }
        finally
        {
            Console.WriteLine($"** SegmentationService.SetImage() FINISHED **");
        }

        return true;
    }

    public async Task<SKBitmap> DoSegmentation(SKPoint location)
    {
        if (_imageEmbeddings == null ||
            _imageWidth == 0 ||
            _imageHeight == 0 ||
            location.IsEmpty)
        {
            return null;
        }

        await _initTask.ConfigureAwait(false);

        try
        {
            // STEP - Use image encoder output (image embeddings) as decoder input

            var decoderInputMeta = _decoderSession.InputMetadata;
            var decoderOutputMeta = _decoderSession.OutputMetadata;

            // TODO - Transform tap coordinates
            var xCoord = location.X;
            var yCoord = location.Y;

            var pointCoords = new DenseTensor<float>(new float[] { xCoord, yCoord, 0, 0 }, new int[] { 1, 2, 2 });
            var pointLabels = new DenseTensor<float>(new float[] { 0, -1 }, new int[] { 1, 2 });
            var maskInput = new DenseTensor<float>(new float[256 * 256], new int[] { 1, 1, 256, 256 });
            var hasMask = new DenseTensor<float>(new float[] { 0 }, new int[] { 1 });
            var originalImageSize = new DenseTensor<float>(new float[] { _imageHeight, _imageWidth }, new int[] { 2 });

            var decoderInputs = new NamedOnnxValue[] {
                    NamedOnnxValue.CreateFromTensor("image_embeddings", _imageEmbeddings),
                    NamedOnnxValue.CreateFromTensor("point_coords", pointCoords),
                    NamedOnnxValue.CreateFromTensor("point_labels", pointLabels),
                    NamedOnnxValue.CreateFromTensor("mask_input", maskInput),
                    NamedOnnxValue.CreateFromTensor("has_mask_input", hasMask),
                    NamedOnnxValue.CreateFromTensor("orig_im_size", originalImageSize)
        };

            // STEP -  Run decoder using inputs

            using (var results = _decoderSession.Run(decoderInputs))
            {
                // Retrieve the output tensor(s)
                var maskTensor = results.First(r => r.Name == "masks").AsTensor<float>();
                var iouPredictionsTensor = results.First(r => r.Name == "iou_predictions").AsTensor<float>();
                var lowResMasksTensor = results.First(r => r.Name == "low_res_masks").AsTensor<float>();

                // Convert the output tensor(s) to a float array
                var mask = maskTensor.ToArray();
                var iouPredictions = iouPredictionsTensor.ToArray();
                var lowResMasks = lowResMasksTensor.ToArray();

                var dimensions = maskTensor.Dimensions.ToArray();

                return GetImageFromMaskTensor(maskTensor);
            }
        }
        catch
        {
            return null;
        }
    }

    private SKBitmap PreprocessImage(SKBitmap bitmap)
    {
        int targetWidth = 1024;
        int targetHeight = 1024;
        var resized = _imageService.GetResizedSKBitmap(bitmap, targetWidth, targetHeight, false, true, false);

        // Convert the color format if necessary
        resized = ConvertColorFormat(resized); // Convert to RGB if needed

        return resized;
    }

    public DenseTensor<float> NormalizeAndPadImageTensor(DenseTensor<float> inputTensor)
    {
        // Perform element-wise subtraction for color normalization
        var x = new DenseTensor<float>(inputTensor.Dimensions);

        for (int i = 0; i < inputTensor.Length; i++)
        {
            var inputValue = inputTensor.GetValue(i);
            var meanValue = meanTensor.GetValue(i % 3); // Use modulo to cycle through mean and std values
            var stdValue = stdTensor.GetValue(i % 3);

            x.SetValue(i, (inputValue - meanValue) / stdValue);
        }

        // Skip padding for now

        /*
        // Get the current height and width
        var w = x.Dimensions[^1];
        var h = x.Dimensions[^2];

        // Calculate padding
        var padh = imgSize - h;
        var padw = imgSize - w;

        // Pad the tensor manually
        x = PadTensor(x, padw, padh);*/

        return x;
    }

    private DenseTensor<float> PadTensor(DenseTensor<float> tensor, int padWidth, int padHeight)
    {
        var paddedDimensions = new int[tensor.Dimensions.Length];
        for (int i = 0; i < tensor.Dimensions.Length; i++)
        {
            paddedDimensions[i] = tensor.Dimensions[i];
        }

        paddedDimensions[^1] += padWidth;
        paddedDimensions[^2] += padHeight;

        var paddedTensor = new DenseTensor<float>(paddedDimensions);

        for (int i = 0; i < tensor.Length; i++)
        {
            var value = tensor.GetValue(i);
            paddedTensor.SetValue(i, value);
        }

        return paddedTensor;
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
        SKBitmap convertedImage = new SKBitmap(image.Width, image.Height, targetColorType, image.AlphaType);

        // Copy the pixels from the original image to the converted image
        image.CopyTo(convertedImage, targetColorType);

        return convertedImage;
    }

    private unsafe DenseTensor<float> ImageToDenseTensor(SKBitmap image)
    {
        var imageDataTensor = new DenseTensor<float>(new int[] { image.Height, image.Width, 3 });

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // Get a pointer to the pixel data
        byte* pixels = (byte*)image.GetPixels().ToPointer();

        int pixelIndex = 0;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                byte pixelR = pixels[pixelIndex++];
                byte pixelG = pixels[pixelIndex++];
                byte pixelB = pixels[pixelIndex++];
                pixelIndex++; // Skip the alpha channel

                imageDataTensor[y, x, 0] = pixelR / (float)255.0;
                imageDataTensor[y, x, 1] = pixelG / (float)255.0;
                imageDataTensor[y, x, 2] = pixelB / (float)255.0;
            }
        }

        stopwatch.Stop();
        Console.WriteLine($"ImageToDenseTensor: {stopwatch.Elapsed.TotalMilliseconds}");

        return imageDataTensor;
    }

    private void checkIfEqualArrays(float[] arrayA, float[] arrayB)
    {
        if (arrayA.Length != arrayB.Length)
        {
            Console.WriteLine($"Arrays are not equal in length. Array A: {arrayA.Length} Array B: {arrayB.Length}");
            return;
        }

        for(var i = 0; i < arrayA.Length; i++)
        {
            if (arrayA[i] != arrayB[i])
            {
                Console.WriteLine($"Arrays values are not the same. Array A[{i}]: {arrayA[i]} Array B[{i}]: {arrayB[i]}");
                return;
            }
        }

        Console.WriteLine($"Arrays values are the same.");
    }

    private SKBitmap GetImageFromMaskTensor(Tensor<float> mask)
    {
        var pixelIndex = 0;

        var result = new SKBitmap(_imageWidth, _imageHeight);
        for (var y = 0; y < _imageHeight; y++)
        {
            for (var x = 0; x < _imageWidth; x++)
            {
                var pixelByte = (byte)(mask[pixelIndex++] * 255f);
                //byte pixelR = (byte)(mask[pixelIndex++] * 255f);
                //byte pixelG = (byte)(mask[pixelIndex++] * 255f);
                //byte pixelB = (byte)(mask[pixelIndex++] * 255f);
                //byte pixelA = (byte)(mask[pixelIndex++] * 255f);

                if (pixelByte > 0)
                {
                    result.SetPixel(x, y, SKColors.Black);
                }
            }
        }

        return result;
    }

}
