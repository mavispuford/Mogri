using Android.Hardware.Lights;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;

namespace MobileDiffusion.ViewModels;

public partial class MaskPageViewModel : PageViewModel, IMaskPageViewModel
{
    private readonly IFileService _fileService;
    private readonly IPopupService _popupService;

    private List<Color> _colorPalette = new();

    [ObservableProperty]
    private Color currentColor = Colors.Black;

    [ObservableProperty]
    private Color paletteIconColor = Colors.White;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private SKBitmap sourceBitmap;

    [ObservableProperty]
    private SKCanvasView sourceCanvasView;

    [ObservableProperty]
    private SKCanvasView maskCanvasView;

    [ObservableProperty]
    private ImageSource savedImageSource;

    public MaskPageViewModel(
        IFileService fileService,
        IPopupService popupService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SourceCanvasView == null ||
            MaskCanvasView == null ||
            SourceBitmap == null)
        {
            await Toast.Make("There is no image to save.").Show();

            return;
        }

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

                        var uri = await _fileService.WriteFileToExternalStorageAsync(fileName, memStream, true);

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

            //SourceBitmap = SKBitmap.Decode(fileStream);

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

            _colorPalette = ExtractColorPalette(SourceBitmap, 40);
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

            PaletteIconColor = CurrentColor.GetLuminosity() > .8 ? Colors.Black : Colors.White;
        }
    }

    unsafe private List<Color> ExtractColorPalette(SKBitmap bitmap, int targetNumber = 40)
    {
        if (bitmap == null)
        {
            return null;
        }

        var initColors = new List<Color>
        {
            Colors.Black,
            Colors.Grey,
            Colors.White,
            Colors.Red,
            Colors.OrangeRed,
            Colors.Pink,
            Colors.Yellow,
            Colors.Green,
            Colors.DarkGreen,
            Colors.Blue,
            Colors.SkyBlue,
            Colors.DarkBlue,
            Colors.Purple,
        };

        initColors.Sort(delegate (Color firstColor, Color nextColor)
        {
            return firstColor.GetHue().CompareTo(nextColor.GetHue());
        });

        var initColorCount = initColors.Count;

        if (targetNumber <= initColorCount)
        {
            return initColors.Take(initColorCount).ToList();
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
            foreach (var initColor in initColors)
            {
                colorsDict.Add(initColor, 0);
            }

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
                // Sort by popularity
                var colorsList = colorsDict.ToList();
                var standardColors = colorsList.GetRange(0, initColorCount).Select(c => c.Key).ToList();
                var otherColors = colorsList.GetRange(initColorCount, colorsDict.Count - initColorCount);

                otherColors.Sort(delegate (KeyValuePair<Color, int> firstPair, KeyValuePair<Color, int> nextPair)
                {
                    return nextPair.Value.CompareTo(firstPair.Value);
                });

                var topColors = otherColors.Take(Math.Min(targetNumber - initColorCount, colorsList.Count)).Select(c => c.Key).ToList();

                // Sort by hue
                topColors.Sort(delegate (Color firstColor, Color nextColor)
                {
                    return firstColor.GetHue().CompareTo(nextColor.GetHue());
                });

                var combinedColors = new List<Color>(standardColors);
                combinedColors.AddRange(topColors);

                return combinedColors;
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

        int width = srcBitmap.Width;       // same for both bitmaps
        int height = srcBitmap.Height;

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

                *resultPtr++ = srcByte1;
                *resultPtr++ = srcByte2;
                *resultPtr++ = srcByte3;
                
                if (mskByte4 != 0)
                { 
                    // Set alpha to a near-zero value
                    *resultPtr++ = (byte)3;
                }
                else
                {
                    *resultPtr++ = srcByte4;
                }

            }
        }

        return resultBitmap;
    }
}
