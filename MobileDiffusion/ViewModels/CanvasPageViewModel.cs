using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Helpers;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;

namespace MobileDiffusion.ViewModels;

public partial class CanvasPageViewModel : PageViewModel, ICanvasPageViewModel
{
    private readonly IFileService _fileService;
    private readonly IImageService _imageService;
    private readonly IPopupService _popupService;

    private List<Color> _colorPalette = new();
    private Color _paletteIconDarkColor = Colors.Black;
    private string _sourceFileName;
    private Random _random = new Random();

    [ObservableProperty]
    private Color currentColor = Colors.Black;

    [ObservableProperty]
    private Color paletteIconColor = Colors.White;

    [ObservableProperty]
    private double initImgRectangleScale;

    [ObservableProperty]
    private float initImgRectangleSize = 256;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private List<MaskLine> lines = new();

    [ObservableProperty]
    private SKBitmap sourceBitmap;

    [ObservableProperty]
    private SKCanvasView sourceCanvasView;

    [ObservableProperty]
    private SKCanvasView maskCanvasView;

    [ObservableProperty]
    private ImageSource savedImageSource;

    [ObservableProperty]
    private SKRect initImgRectangle;

    [ObservableProperty]
    private bool showInitImgRectangle;

    [ObservableProperty]
    private IAsyncRelayCommand prepareForSavingCommand;

    public CanvasPageViewModel(
        IFileService fileService,
        IPopupService popupService,
        IImageService imageService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));

        Application.Current.Resources.TryGetValue("IndependenceAccent", out var independenceColor);

        if (independenceColor is Color paletteIconDarkColor)
        {
            _paletteIconDarkColor = paletteIconDarkColor;
        }
    }

    partial void OnCurrentColorChanged(Color value)
    {
        if (value != null)
        {
            PaletteIconColor = value.GetLuminosity() > .8 ? _paletteIconDarkColor : Colors.White;
        }
    }

    partial void OnSourceBitmapChanged(SKBitmap value)
    {
        if (value != null)
        {
            _colorPalette = ExtractColorPalette(value, 30);
        }
    }

    [RelayCommand]
    private async Task SaveMask()
    {
        if (string.IsNullOrEmpty(_sourceFileName))
        {
            await Toast.Make("There is no image loaded to associate with this mask.").Show();

            return;
        }

        try
        {
            var maskUri = await _fileService.WriteMaskFileToAppDataAsync(_sourceFileName, new Mask { Lines = Lines });

            await Toast.Make("Mask saved.").Show();
        }
        catch (Exception e)
        {
            await Toast.Make("Failed to save mask file. Please try again.").Show();
        }
    }

    [RelayCommand]
    private async Task SaveImage()
    {
        if (SourceCanvasView == null ||
            MaskCanvasView == null ||
            SourceBitmap == null)
        {
            await Toast.Make("There is no image to save.").Show();

            return;
        }

        await PrepareForSavingCommand?.ExecuteAsync(FinishSavingCommand);
    }

    [RelayCommand]
    private async Task BeginCropImageRect()
    {
        if (SourceCanvasView == null ||
            MaskCanvasView == null ||
            SourceBitmap == null)
        {
            await Toast.Make("There is no image data to crop.").Show();

            return;
        }

        await PrepareForSavingCommand?.ExecuteAsync(FinishCroppingInitImgRectangleCommand);
    }

    [RelayCommand]
    private async Task FinishSaving()
    {
        IsBusy = true;

        await Task.Run(async () =>
        {
            var maskCapture = await MaskCanvasView.CaptureAsync();
            var maskStream = await maskCapture.OpenReadAsync();
            var maskBitmap = SKBitmap.Decode(maskStream);

            try
            {
                // Colorize the source bitmap using the mask, then create a black and white mask
                var colorizedBitmap = CreateMaskedBitmap(SourceBitmap, maskBitmap);
                var blackAndWhiteMaskBitmap = CreateBlackAndWhiteMask(maskBitmap);

                //var fileName = $"Mask-{DateTime.Now.Ticks}.png";

                using (var maskMemStream = new MemoryStream())
                {
                    using (var maskSkiaStream = new SKManagedWStream(maskMemStream))
                    {
                        blackAndWhiteMaskBitmap.Encode(maskSkiaStream, SKEncodedImageFormat.Png, 100);

                        //maskedResultBitmap.Encode(skiaStream, SKEncodedImageFormat.Png, 100);

                        maskMemStream.Seek(0, SeekOrigin.Begin);

                        //var uri = await _fileService.WriteImageFileToExternalStorageAsync(fileName, maskMemStream, true);

                        
                        maskMemStream.Seek(0, SeekOrigin.Begin);
                        var maskImageBytes = maskMemStream.ToArray();
                        var maskImageString = Convert.ToBase64String(maskImageBytes);

                        var maskImgContentTypeString = string.Format(Constants.ImageDataFormat, "image/png", maskImageString);

                        using var colorizedMemStream = new MemoryStream();
                        using var colorizedSkiaStream = new SKManagedWStream(colorizedMemStream);
                        colorizedBitmap.Encode(colorizedSkiaStream, SKEncodedImageFormat.Png, 100);
                        colorizedMemStream.Seek(0, SeekOrigin.Begin);
                        var colorizedImageBytes = colorizedMemStream.ToArray();
                        var colorizedImageString = Convert.ToBase64String(colorizedImageBytes);

                        var colorizedImgContentTypeString = string.Format(Constants.ImageDataFormat, "image/png", colorizedImageString);

                        var parameters = new Dictionary<string, object>
                        {
                            {NavigationParams.InitImgString, colorizedImgContentTypeString },
                            {NavigationParams.MaskImgString, maskImgContentTypeString }
                        };

                        var dispatcher = Shell.Current.CurrentPage.Dispatcher;
                        await dispatcher?.DispatchAsync(async () =>
                        {
                            await Shell.Current.GoToAsync("///MainPageTab", parameters);
                        });
                    }
                }
            }
            catch (Exception e)
            {
                //
            }
        });

        IsBusy = false;
    }

    [RelayCommand]
    private async Task FinishCroppingInitImgRectangle()
    {
        IsBusy = true;

        await Task.Run(async () =>
        {
            var maskCapture = await MaskCanvasView.CaptureAsync();
            var maskStream = await maskCapture.OpenReadAsync();
            var maskBitmap = SKBitmap.Decode(maskStream);

            try
            {
                var maskedResultBitmap = CreateMaskedBitmap(SourceBitmap, maskBitmap);

                var croppedBitmap = GetCroppedBitmap(maskedResultBitmap, InitImgRectangle);

                using (var memStream = new MemoryStream())
                {
                    using (var skiaStream = new SKManagedWStream(memStream))
                    {
                        croppedBitmap.Encode(skiaStream, SKEncodedImageFormat.Png, 100);

                        memStream.Seek(0, SeekOrigin.Begin);

                        var imageBytes = memStream.ToArray();
                        var imageString = Convert.ToBase64String(imageBytes);

                        var contentTypeString = string.Format(Constants.ImageDataFormat, "image/png", imageString);

                        var parameters = new Dictionary<string, object>
                        {
                            { NavigationParams.ImageWidth, InitImgRectangleSize },
                            { NavigationParams.ImageHeight, InitImgRectangleSize },
                            { NavigationParams.InitImgString, contentTypeString },
                        };

                        var dispatcher = Shell.Current.CurrentPage.Dispatcher;
                        await dispatcher?.DispatchAsync(async () =>
                        {
                            await Shell.Current.GoToAsync("///MainPageTab", parameters);

                            await Toast.Make("Section has been cropped and set as source image.").Show();
                        });
                    }
                }
            }
            catch (Exception e)
            {
                //
            }
        });

        IsBusy = false;
    }

    [RelayCommand]
    private async Task ShowMediaPicker()
    {
        var fileResult = await MediaPicker.PickPhotoAsync();

        if (fileResult == null)
        {
            return;
        }

        using var fileStream = await fileResult.OpenReadAsync();

        await LoadSourceBitmapUsingStream(fileStream, fileResult.FileName);
    }

    private async Task LoadSourceBitmapUsingStream(Stream stream, string fileName)
    {
        try
        {
            IsBusy = true;

            // Instead of a simple SKBitmap.Decode() call, we're using a codec and SKImageInfo with Unpremul for the
            // AlphaType so masked images can be reopened after being created

            var codec = SKCodec.Create(stream);
            var info = new SKImageInfo
            {
                AlphaType = SKAlphaType.Unpremul,
                ColorSpace = codec.Info.ColorSpace,
                ColorType = codec.Info.ColorType,
                Height = codec.Info.Height,
                Width = codec.Info.Width,
            };

            var sourceBitmap = SKBitmap.Decode(codec, info);

            // Wrap in dispatch call because ApplyQueryAttributes can call this method and it
            // appears to be called from a non-UI thread.
            var dispatcher = Dispatcher.GetForCurrentThread();
            await dispatcher.DispatchAsync(() =>
            {
                SourceBitmap = sourceBitmap;
            });

            _sourceFileName = fileName;

            var mask = await _fileService.GetMaskFileFromAppDataAsync(_sourceFileName);

            if (mask?.Lines != null)
            {
                await dispatcher.DispatchAsync(() =>
                {
                    Lines = mask.Lines;
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            // TODO - Handle exceptions
        }
        finally
        {
            await stream.DisposeAsync();

            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ShowColorPicker()
    {
        var parameters = new Dictionary<string, object> {
            { NavigationParams.Color, CurrentColor },
            { NavigationParams.ColorPalette, _colorPalette },
        };

        var color = await _popupService.ShowPopupAsync("ColorPickerPopup", parameters) as Color;

        if (color != null)
        {
            CurrentColor = color;
        }
    }

    [RelayCommand]
    private void ToggleInitImgRectangle()
    {
        if (!ShowInitImgRectangle)
        {
            ShowInitImgRectangle = true;
        }

        InitImgRectangleSize = InitImgRectangleSize switch 
        {
            0f => 256f,
            256f => 512f,
            512f => 768f,
            768f => 1024f,
            1024f => 0f
        };

        if (InitImgRectangleSize == 0f)
        {
            ShowInitImgRectangle = false;
        }
    }

    unsafe private List<Color> ExtractColorPalette(SKBitmap bitmap, int targetNumber = 30)
    {
        if (bitmap == null)
        {
            return null;
        }

        var smallBitmap = bitmap.Resize(new SKSizeI(16, 16), SKFilterQuality.None);

        SKColorType colorType = smallBitmap.ColorType;

        var width = smallBitmap.Width;
        var height = smallBitmap.Height;

        var tolerance = .4f;
        var iteration = 0;

        // Start with more tolerance, and decrease it each iteration until we get a palette with the requested
        // number of swatches

        do
        {
            var colorsDict = new Dictionary<Color, int>();

            if (iteration > 0)
            {
                tolerance /= (2 * iteration);
            }

            iteration++;

            byte* bitmapPtr = (byte*)smallBitmap.GetPixels().ToPointer();

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    // Get color from bitmap
                    byte byte1 = *bitmapPtr++;         // red or blue
                    byte byte2 = *bitmapPtr++;         // green
                    byte byte3 = *bitmapPtr++;         // blue or red
                    byte byte4 = *bitmapPtr++;         // alpha

                    Color color = null;

                    if (colorType == SKColorType.Rgba8888)
                    {
                        color = Color.FromRgba(byte1, byte2, byte3, (byte)255);
                    }
                    else if (colorType == SKColorType.Bgra8888)
                    {
                        color = Color.FromRgba(byte3, byte2, byte1, (byte)255);
                    }

                    if (color == null)
                    {
                        continue;
                    }

                    var matchingColor =
                        colorsDict.Keys.FirstOrDefault(c => Math.Abs(c.GetHue() - color.GetHue()) < tolerance);

                    if (matchingColor != null)
                    {
                        colorsDict[matchingColor]++;
                    }
                    else
                    {
                        colorsDict[color] = 1;
                    }
                }
            }

            if (iteration > 10 || colorsDict.Count >= targetNumber)
            {
                // Sort all by popularity
                var sortedByPopularity = colorsDict.ToList();
                sortedByPopularity.Sort(delegate (KeyValuePair<Color, int> firstPair, KeyValuePair<Color, int> nextPair)
                {
                    return nextPair.Value.CompareTo(firstPair.Value);
                });

                var topColors = sortedByPopularity.Take(Math.Min(targetNumber, sortedByPopularity.Count)).Select(c => c.Key).ToList();

                // Sort final selection by hue
                topColors.Sort(delegate (Color firstColor, Color nextColor)
                {
                    return firstColor.GetHue().CompareTo(nextColor.GetHue());
                });

                return topColors;
            }   
        } while (true);
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
    }

    public override async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);
        
        if (query.TryGetValue(NavigationParams.CanvasImageString, out var canvasImageString) &&
            canvasImageString is string byteString)
        {
            if (ShowInitImgRectangle)
            {
                await BeginStitchingAsync(byteString);
            }
            else
            {
                using var stream = await _imageService.GetStreamFromContentTypeStringAsync(byteString, new CancellationTokenSource().Token);

                SourceBitmap = SKBitmap.Decode(stream);
            }
        }

        if (query.TryGetValue(NavigationParams.AppShareFileUri, out var imageUriFromAppShareParam) &&
            imageUriFromAppShareParam is string imageUri)
        {
            using var stream = await _fileService.GetFileStreamUsingExactUriAsync(imageUri);

            await LoadSourceBitmapUsingStream(stream, Path.GetFileName(imageUri));
        }

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    private async Task BeginStitchingAsync(string byteString)
    {
        var tokenSource = new CancellationTokenSource();

        using var stream = await _imageService.GetStreamFromContentTypeStringAsync(byteString, tokenSource.Token);

        var stitchBitmap = SKBitmap.Decode(stream);

        var finalBitmap = StitchBitmapIntoSource(SourceBitmap, stitchBitmap, InitImgRectangle);

        SourceBitmap = finalBitmap;
    }

    private unsafe SKBitmap CreateBlackAndWhiteMask(SKBitmap maskBitmap)
    {
        if (maskBitmap == null)
        {
            return null;
        }

        byte* mskPtr = (byte*)maskBitmap.GetPixels().ToPointer();

        var width = maskBitmap.Width;
        var height = maskBitmap.Height;

        SKColorType typeMsk = maskBitmap.ColorType;

        var resultBitmap = new SKBitmap(width, height, typeMsk, SKAlphaType.Unpremul);

        byte* resultPtr = (byte*)resultBitmap.GetPixels().ToPointer();

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                // Get color from mask bitmap
                byte mskByte1 = *mskPtr++;         // red or blue
                byte mskByte2 = *mskPtr++;         // green
                byte mskByte3 = *mskPtr++;         // blue or red
                byte mskByte4 = *mskPtr++;         // alpha

                if (mskByte4 != 0)
                {
                    var maskColor = new Color();

                    if (typeMsk == SKColorType.Rgba8888)
                    {
                        maskColor = Color.FromRgba(mskByte1, mskByte2, mskByte3, mskByte4);
                    }
                    else if (typeMsk == SKColorType.Bgra8888)
                    {
                        maskColor = Color.FromRgba(mskByte3, mskByte2, mskByte1, mskByte4);
                    }

                    float strength = mskByte4 / 255f;

                    var newColor = Color.FromRgba(1f, 1f, 1f, strength);

                    *resultPtr++ = typeMsk == SKColorType.Rgba8888 ? newColor.GetByteRed() : newColor.GetByteBlue();
                    *resultPtr++ = newColor.GetByteGreen();
                    *resultPtr++ = typeMsk == SKColorType.Rgba8888 ? newColor.GetByteBlue() : newColor.GetByteRed();
                    *resultPtr++ = newColor.GetByteAlpha();
                }
                else
                {
                    *resultPtr++ = mskByte1;
                    *resultPtr++ = mskByte2;
                    *resultPtr++ = mskByte3;
                    *resultPtr++ = mskByte4;
                }

            }
        }

        return resultBitmap;
    }

    private unsafe SKBitmap CreateMaskedBitmap(SKBitmap srcBitmap, SKBitmap maskBitmapFull, bool randomizeMaskPixels = true)
    {
        if (srcBitmap == null ||
            maskBitmapFull == null)
        {
            return null;
        }

        var maskBitmap = maskBitmapFull.Resize(srcBitmap.Info, SKFilterQuality.None);

        byte* srcPtr = (byte*)srcBitmap.GetPixels().ToPointer();
        byte* mskPtr = (byte*)maskBitmap.GetPixels().ToPointer();

        var width = srcBitmap.Width;       // same for both bitmaps
        var height = srcBitmap.Height;

        SKColorType typeSrc = srcBitmap.ColorType;
        SKColorType typeMsk = maskBitmap.ColorType;

        var resultBitmap = new SKBitmap(width, height, typeSrc, SKAlphaType.Unpremul);
        
        byte* resultPtr = (byte*)resultBitmap.GetPixels().ToPointer();

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                // Get color from source bitmap
                byte srcByte1 = *srcPtr++;         // red or blue
                byte srcByte2 = *srcPtr++;         // green
                byte srcByte3 = *srcPtr++;         // blue or red
                byte srcByte4 = *srcPtr++;         // alpha

                // Get color from mask bitmap
                byte mskByte1 = *mskPtr++;         // red or blue
                byte mskByte2 = *mskPtr++;         // green
                byte mskByte3 = *mskPtr++;         // blue or red
                byte mskByte4 = *mskPtr++;         // alpha

                if (mskByte4 != 0)
                {
                    var sourceColor = new Color();

                    if (typeSrc == SKColorType.Rgba8888)
                    {
                        sourceColor = Color.FromRgba(srcByte1, srcByte2, srcByte3, srcByte4);
                    }
                    else if (typeSrc == SKColorType.Bgra8888)
                    {
                        sourceColor = Color.FromRgba(srcByte3, srcByte2, srcByte1, srcByte4);
                    }

                    var maskColor = new Color();

                    // This adds randomness to each pixel to add texture to masked portions,
                    // resulting in a better image overall when processed.
                    if (randomizeMaskPixels)
                    {
                        // This controls how far away each pixel can travel from the original value
                        const int rngAmount = 100;

                        var pos = _random.Next(0, 1) == 1;
                        mskByte1 = (byte)MathHelper.Clamp(((int)mskByte1) + (_random.Next(0, rngAmount) * (pos ? 1 : -1)), 0, 255);

                        pos = _random.Next(0, 1) == 1;
                        mskByte2 = (byte)MathHelper.Clamp(((int)mskByte2) + (_random.Next(0, rngAmount) * (pos ? 1 : -1)), 0, 255);
                        
                        pos = _random.Next(0, 1) == 1;
                        mskByte3 = (byte)MathHelper.Clamp(((int)mskByte3) + (_random.Next(0, rngAmount) * (pos ? 1 : -1)), 0, 255);
                    }

                    if (typeMsk == SKColorType.Rgba8888)
                    {
                        maskColor = Color.FromRgba(mskByte1, mskByte2, mskByte3, mskByte4);
                    }
                    else if (typeMsk == SKColorType.Bgra8888)
                    {
                        maskColor = Color.FromRgba(mskByte3, mskByte2, mskByte1, mskByte4);
                    }

                    // Limit the strength to preserve some of the pixel data from the underlying image
                    float strength = Math.Min(mskByte4, (byte)204) / 255f;

                    // Interpolate between the source and mask colors using the strength
                    var newColor = Color.FromRgba(
                        sourceColor.Red + strength * (maskColor.Red - sourceColor.Red),
                        sourceColor.Green + strength * (maskColor.Green - sourceColor.Green),
                        sourceColor.Blue + strength * (maskColor.Blue - sourceColor.Blue),
                        1f);

                    *resultPtr++ = typeMsk == SKColorType.Rgba8888 ? newColor.GetByteRed() : newColor.GetByteBlue();
                    *resultPtr++ = newColor.GetByteGreen();
                    *resultPtr++ = typeMsk == SKColorType.Rgba8888 ? newColor.GetByteBlue() : newColor.GetByteRed();
                    *resultPtr++ = newColor.GetByteAlpha();

                    // Set alpha to a near-zero value for InvokeAI
                    //*resultPtr++ = 3;
                }
                else
                {
                    *resultPtr++ = srcByte1;
                    *resultPtr++ = srcByte2;
                    *resultPtr++ = srcByte3;
                    *resultPtr++ = srcByte4;
                }

            }
        }

        return resultBitmap;
    }

    private SKBitmap GetCroppedBitmap(SKBitmap bitmap, SKRect cropRect)
    {
        if (bitmap == null)
        {
            return null;
        }

        if (cropRect.Width <= 0 ||
            cropRect.Height <= 0)
        {
            return bitmap;
        }

        var left = (float)(cropRect.Left * InitImgRectangleScale);
        var top = (float)(cropRect.Top * InitImgRectangleScale);

        var adjustedRect = new SKRect(
            left, 
            top,
            left + InitImgRectangleSize, 
            top + InitImgRectangleSize);
        
        var info = new SKImageInfo
        {
            AlphaType = SKAlphaType.Unpremul,
            ColorSpace = bitmap.ColorSpace,
            ColorType = bitmap.ColorType,
            Height = (int)InitImgRectangleSize,
            Width = (int)InitImgRectangleSize,
        };

        var croppedBitmap = new SKBitmap(info);

        var source = new SKRect(adjustedRect.Left, adjustedRect.Top,
                                   adjustedRect.Right, adjustedRect.Bottom);
        var dest = new SKRect(0, 0, adjustedRect.Width, adjustedRect.Height);

        using (var canvas = new SKCanvas(croppedBitmap))
        {
            var paint = new SKPaint()
            {
                FilterQuality = SKFilterQuality.None,
                IsAntialias = false,
            };

            canvas.DrawBitmap(bitmap, source, dest, paint);
        }

        return croppedBitmap;
    }

    private SKBitmap StitchBitmapIntoSource(SKBitmap bitmap, SKBitmap bitmapToStitchIn, SKRect rect)
    {
        if (bitmap == null)
        {
            return null;
        }

        if (bitmapToStitchIn == null)
        {
            return bitmap;
        }

        var info = new SKImageInfo
        {
            AlphaType = bitmap.AlphaType,
            ColorSpace = bitmap.ColorSpace,
            ColorType = bitmap.ColorType,
            Height = bitmap.Height,
            Width = bitmap.Width,
        };

        var resultBitmap = new SKBitmap(info);

        SKRect adjustedRect;
        
        if (rect.Width == 0 ||
            rect.Height == 0)
        {
            adjustedRect = bitmap.Info.Rect;
        }
        else
        {
            adjustedRect = new SKRect(
                (float)(rect.Left * InitImgRectangleScale),
                (float)(rect.Top * InitImgRectangleScale),
                (float)(rect.Right * InitImgRectangleScale),
                (float)(rect.Bottom * InitImgRectangleScale));
        }

        var source = new SKRect(0, 0, adjustedRect.Width, adjustedRect.Height);
        var dest = new SKRect(adjustedRect.Left, adjustedRect.Top, adjustedRect.Right, adjustedRect.Bottom);

        using (var canvas = new SKCanvas(resultBitmap))
        {
            var paint = new SKPaint
            {
                FilterQuality = SKFilterQuality.High,
                IsAntialias = true,
            };

            canvas.DrawBitmap(bitmap, 0, 0);

            // Scale it if the size doesn't match the current init image rectangle
            var toStitch = bitmapToStitchIn.Width != dest.Width || bitmapToStitchIn.Height != dest.Height ?
                bitmapToStitchIn.Resize(adjustedRect.Size.ToSizeI(), SKFilterQuality.High) :
                bitmapToStitchIn;

            canvas.DrawBitmap(toStitch, source, dest);
        }

        return resultBitmap;
    }
}
