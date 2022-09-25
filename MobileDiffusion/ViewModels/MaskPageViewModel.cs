using Android.Hardware.Lights;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;

namespace MobileDiffusion.ViewModels;

public partial class MaskPageViewModel : PageViewModel, IMaskPageViewModel
{
    private readonly IFileService _fileService;
    private readonly IPopupService _popupService;

    private List<Color> _colorPalette = new();
    private Color _paletteIconDarkColor = Colors.Black;
    private string _sourceFileName;

    [ObservableProperty]
    private Color currentColor = Colors.Black;

    [ObservableProperty]
    private Color paletteIconColor = Colors.White;

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

    public MaskPageViewModel(
        IFileService fileService,
        IPopupService popupService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));

        Application.Current.Resources.TryGetValue("IndependenceAccent", out var independenceColor);

        if (independenceColor is Color paletteIconDarkColor)
        {
            _paletteIconDarkColor = paletteIconDarkColor;
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

        await PrepareForSavingCommand?.ExecuteAsync(null);
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
                var maskedResultBitmap = CreateMaskedBitmap(SourceBitmap, maskBitmap);

                var fileName = $"Mask-{DateTime.Now.Ticks}.png";

                using (var memStream = new MemoryStream())
                {
                    using (var skiaStream = new SKManagedWStream(memStream))
                    {
                        maskedResultBitmap.Encode(skiaStream, SKEncodedImageFormat.Png, 100);

                        memStream.Seek(0, SeekOrigin.Begin);

                        var uri = await _fileService.WriteImageFileToExternalStorageAsync(fileName, memStream, true);

                        var useAsInitImg = false;

                        var dispatcher = Shell.Current.CurrentPage.Dispatcher;
                        await dispatcher?.DispatchAsync(async () =>
                        {
                            useAsInitImg = await Shell.Current.CurrentPage.DisplayAlert("Use as Source Image?",
                                $"Image successfully saved as:\n{fileName}\n\nWould you like to use it as the source image?",
                                "Use Image",
                                "Close");
                        });

                        if (useAsInitImg)
                        {
                            memStream.Seek(0, SeekOrigin.Begin);
                            var imageBytes = memStream.ToArray();
                            var imageString = Convert.ToBase64String(imageBytes);

                            var contentTypeString = string.Format(Constants.ImageDataFormat, "image/png", imageString);

                            var parameters = new Dictionary<string, object>
                            {
                                {NavigationParams.InitImgString, contentTypeString }
                            };

                            await dispatcher?.DispatchAsync(async () =>
                            {
                                await Shell.Current.GoToAsync("///MainPageTab", parameters);
                            });
                        }
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
        try
        {
            var fileResult = await MediaPicker.PickPhotoAsync();

            if (fileResult == null)
            {
                return;
            }

            IsBusy = true;

            using var fileStream = await fileResult.OpenReadAsync();

            // Instead of a simple SKBitmap.Decode() call, we're using a codec and SKImageInfo with Unpremul for the
            // AlphaType so masked images can be reopened after being created

            var codec = SKCodec.Create(fileStream);
            var info = new SKImageInfo
            {
                AlphaType = SKAlphaType.Unpremul,
                ColorSpace = codec.Info.ColorSpace,
                ColorType = codec.Info.ColorType,
                Height = codec.Info.Height,
                Width = codec.Info.Width,
            };

            SourceBitmap = SKBitmap.Decode(codec, info);

            _sourceFileName = fileResult.FileName;

            var mask = await _fileService.GetMaskFileFromAppDataAsync(_sourceFileName);

            if (mask?.Lines != null)
            {
                Lines = mask.Lines;
            }

            _colorPalette = ExtractColorPalette(SourceBitmap, 30);
        }
        catch
        {
            // TODO - Handle exceptions
        }
        finally
        {
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

            PaletteIconColor = CurrentColor.GetLuminosity() > .8 ? _paletteIconDarkColor : Colors.White;
        }
    }

    [RelayCommand]
    private void ToggleInitImgRectangle()
    {
        ShowInitImgRectangle = !ShowInitImgRectangle;
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

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);
    }

    unsafe SKBitmap CreateMaskedBitmap(SKBitmap srcBitmap, SKBitmap maskBitmapFull)
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
                    // Limit the strength to preserve some of the pixel data from the underlying image
                    float strength = Math.Min(mskByte4 / 255f, 204);

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

                    if (typeMsk == SKColorType.Rgba8888)
                    {
                        maskColor = Color.FromRgba(mskByte1, mskByte2, mskByte3, mskByte4);
                    }
                    else if (typeMsk == SKColorType.Bgra8888)
                    {
                        maskColor = Color.FromRgba(mskByte3, mskByte2, mskByte1, mskByte4);
                    }

                    var newColor = Color.FromRgba(
                        sourceColor.Red + strength * (maskColor.Red - sourceColor.Red),
                        sourceColor.Green + strength * (maskColor.Green - sourceColor.Green),
                        sourceColor.Blue + strength * (maskColor.Blue - sourceColor.Blue),
                        1f);

                    *resultPtr++ = typeMsk == SKColorType.Rgba8888 ? newColor.GetByteRed() : newColor.GetByteBlue();
                    *resultPtr++ = newColor.GetByteGreen();
                    *resultPtr++ = typeMsk == SKColorType.Rgba8888 ? newColor.GetByteBlue() : newColor.GetByteRed();

                    // Set alpha to a near-zero value
                    *resultPtr++ = 3;
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

        var croppedBitmap = new SKBitmap((int)cropRect.Width,
                                              (int)cropRect.Height);
        var dest = new SKRect(0, 0, cropRect.Width, cropRect.Height);
        var source = new SKRect(cropRect.Left, cropRect.Top,
                                   cropRect.Right, cropRect.Bottom);

        using (var canvas = new SKCanvas(croppedBitmap))
        {
            canvas.DrawBitmap(bitmap, source, dest);
        }

        return croppedBitmap;
    }
}
