using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Enums;
using MobileDiffusion.Helpers;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;
using System.Collections.ObjectModel;
using ColorMine.ColorSpaces;
using ColorMine.ColorSpaces.Comparisons;

namespace MobileDiffusion.ViewModels;

public partial class CanvasPageViewModel : PageViewModel, ICanvasPageViewModel
{
    private readonly object _setSegmentationImageLock = new();
    private readonly IFileService _fileService;
    private readonly IImageService _imageService;
    private readonly IPopupService _popupService;
    private readonly ISegmentationService _segmentationService;

    private int _imgRectIndex = 0;
    private List<int> _supportedImgRectSizes = new()
    {
        256,512,768,1024,1280,2048
    };

    private List<Color> _colorPalette = new();
    private Color _paletteIconDarkColor = Colors.Black;
    private string _sourceFileName;
    private Random _random = new Random();
    private bool _doingSegmentation = false;
    private CancellationTokenSource _setSegmentationImageCancellationTokenSource;
    private int _setSegmentationImageRequestCount = 0;

    [ObservableProperty]
    private List<IPaintingToolViewModel> _availableTools = new();

    [ObservableProperty]
    private IPaintingToolViewModel _currentTool;

    [ObservableProperty]
    private float _currentAlpha = .5f;

    [ObservableProperty]
    private Color _currentColor = Colors.Black;

    [ObservableProperty]
    private Color _paletteIconColor = Colors.White;

    [ObservableProperty]
    private double _initImgRectangleScale;

    [ObservableProperty]
    private float _initImgRectangleSize;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private ObservableCollection<CanvasActionViewModel> _canvasActions = new();

    [ObservableProperty]
    private SKBitmap _sourceBitmap;

    [ObservableProperty]
    private SKBitmap _segmentationBitmap;

    [ObservableProperty]
    private SKCanvasView _sourceCanvasView;

    [ObservableProperty]
    private SKCanvasView _maskCanvasView;

    [ObservableProperty]
    private ImageSource _savedImageSource;

    [ObservableProperty]
    private SKRect _initImgRectangle;

    [ObservableProperty]
    private bool _showInitImgRectangle;

    [ObservableProperty]
    private bool _showMaskLayer = true;

    [ObservableProperty]
    private bool _settingSegmentationImage = false;

    [ObservableProperty]
    private bool _hasSegmentationImage = false;

    [ObservableProperty]
    private bool _showContextMenu = false;

    [ObservableProperty]
    private bool _gettingColorPalette = false;

    [ObservableProperty]
    private SegmentationMode _segmentationMode = SegmentationMode.AddArea;

    [ObservableProperty]
    private IAsyncRelayCommand _prepareForSavingCommand;

    public CanvasPageViewModel(
        IFileService fileService,
        IPopupService popupService,
        IImageService imageService,
        ISegmentationService segmentationService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _segmentationService = segmentationService ?? throw new ArgumentNullException(nameof(segmentationService));

        Application.Current.Resources.TryGetValue("IndependenceAccent", out var independenceColor);

        if (independenceColor is Color paletteIconDarkColor)
        {
            _paletteIconDarkColor = paletteIconDarkColor;
        }

        InitImgRectangleSize = _supportedImgRectSizes[_imgRectIndex];

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Brush",
            IconCode = "\ue3ae",
            Effect = MaskEffect.Paint,
            Type = ToolType.PaintBrush
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Eraser",
            IconCode = "\ue6d0",
            Effect = MaskEffect.Erase,
            Type = ToolType.Eraser
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Paint Bucket",
            IconCode = "\ue997",
            Effect = MaskEffect.Erase,
            Type = ToolType.PaintBucket
        });

        CurrentTool = AvailableTools.FirstOrDefault();
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
            _ = Task.Run(async () =>
            {
                GettingColorPalette = true;

                _colorPalette = ExtractColorPalette(value, 48);

                GettingColorPalette = false;

                var paintBucketTool = AvailableTools.FirstOrDefault(t => t.Type == ToolType.PaintBucket);

                lock (_setSegmentationImageLock)
                {
                    _setSegmentationImageRequestCount++;

                    paintBucketTool.IsLoading = true;
                    SettingSegmentationImage = true;
                }

                if (!_setSegmentationImageCancellationTokenSource?.IsCancellationRequested ?? false)
                {
                    _setSegmentationImageCancellationTokenSource.Cancel();
                }

                _setSegmentationImageCancellationTokenSource = new CancellationTokenSource();

                HasSegmentationImage = await _segmentationService.SetImage(value, _setSegmentationImageCancellationTokenSource?.Token ?? CancellationToken.None);

                lock (_setSegmentationImageLock)
                {
                    _setSegmentationImageRequestCount--;

                    paintBucketTool.IsLoading = _setSegmentationImageRequestCount > 0;
                    SettingSegmentationImage = _setSegmentationImageRequestCount > 0;
                }

            });
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        const string maskChoice = "Mask";
        const string imageChoice = "Image";

        var selection = string.Empty;

        var dispatcher = Shell.Current.CurrentPage.Dispatcher;
        await dispatcher?.DispatchAsync(async () =>
        {
            selection = await Shell.Current.DisplayActionSheet("Save?", "Cancel", null, maskChoice, imageChoice);
        });

        switch (selection)
        {
            case maskChoice:
                await saveMask();
                break;
            case imageChoice:
                await saveImage();
                break;
        }
    }

    private async Task saveMask()
    {
        if (string.IsNullOrEmpty(_sourceFileName))
        {
            await Toast.Make("There is no image loaded to associate with this mask.").Show();

            return;
        }

        try
        {
            var maskUri = await _fileService.WriteMaskFileToAppDataAsync(_sourceFileName, 
                new MaskViewModel { 
                    Lines = CanvasActions.Where(ca => ca is MaskLineViewModel).Select(ml => (MaskLineViewModel)ml).ToList() 
                });

            await Toast.Make("Mask saved.").Show();
        }
        catch (Exception e)
        {
            await Toast.Make("Failed to save mask file. Please try again.").Show();
        }
    }

    private async Task saveImage()
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
    private async Task SendToImageToImage()
    {
        if (SourceCanvasView == null ||
            MaskCanvasView == null ||
            SourceBitmap == null)
        {
            await Toast.Make("There is no image to send.").Show();

            return;
        }

        await PrepareForSavingCommand?.ExecuteAsync(FinishSendingToImageToImageCommand);
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
            var dispatcher = Shell.Current.CurrentPage.Dispatcher;

            try
            {
                using var memStream = new MemoryStream();
                using var skiaStream = new SKManagedWStream(memStream);

                SourceBitmap.Encode(skiaStream, SKEncodedImageFormat.Png, 100);

                var fileName = $"CanvasImage-{DateTime.Now.Ticks}.png";
                memStream.Seek(0, SeekOrigin.Begin);
                var uri = await _fileService.WriteImageFileToExternalStorageAsync(fileName, memStream, false);

                await dispatcher?.DispatchAsync(async () =>
                {
                    await Toast.Make($"{fileName} saved.").Show();
                });
            }
            catch (Exception e)
            {
                await dispatcher?.DispatchAsync(async () =>
                {
                    await Toast.Make("Failed to save image. Please try again.").Show();
                });
            }
        });

        IsBusy = false;
    }

    [RelayCommand]
    private async Task FinishSendingToImageToImage()
    {
        IsBusy = true;

        await Task.Run(async () =>
        {
            var maskCapture = await MaskCanvasView.CaptureAsync();
            var maskStream = await maskCapture.OpenReadAsync();
            var maskBitmap = SKBitmap.Decode(maskStream);
            var sameSizeMaskBitmap = maskBitmap.Resize(SourceBitmap.Info, SKFilterQuality.High);

            try
            {
                // Colorize the source bitmap using the mask, then create a black and white mask
                var colorizedBitmap = CreateMaskedBitmap(SourceBitmap, sameSizeMaskBitmap);
                using var colorizedMemStream = new MemoryStream();
                using var colorizedSkiaStream = new SKManagedWStream(colorizedMemStream);
                colorizedBitmap.Encode(colorizedSkiaStream, SKEncodedImageFormat.Png, 100);
                colorizedMemStream.Seek(0, SeekOrigin.Begin);
                var colorizedImageBytes = colorizedMemStream.ToArray();
                var colorizedImageString = Convert.ToBase64String(colorizedImageBytes);

                var colorizedImgContentTypeString = string.Format(Constants.ImageDataFormat, "image/png", colorizedImageString);

                // Attempt to match the aspect ratio of the image within the resolution constraints
                var constrainedDimensions = MathHelper.GetAspectCorrectConstrainedDimensions(colorizedBitmap.Width, colorizedBitmap.Height, 0, 0, MathHelper.DimensionConstraint.ClosestMatch);

                var parameters = new Dictionary<string, object>
                {
                    { NavigationParams.ImageWidth, constrainedDimensions.Width },
                    { NavigationParams.ImageHeight, constrainedDimensions.Height },
                    { NavigationParams.InitImgString, colorizedImgContentTypeString }
                };

                var maskImgContentTypeString = string.Empty;

                if (CanvasActions.Any(c => c.CanvasActionType == CanvasActionType.Mask))
                {
                    if (maskBitmap.Pixels.Any(p => p.Alpha > 0))
                    {
                        var blackAndWhiteMaskBitmap = CreateBlackAndWhiteMask(sameSizeMaskBitmap);
                        using var maskMemStream = new MemoryStream();
                        using var maskSkiaStream = new SKManagedWStream(maskMemStream);
                        blackAndWhiteMaskBitmap.Encode(maskSkiaStream, SKEncodedImageFormat.Png, 100);
                        maskMemStream.Seek(0, SeekOrigin.Begin);
                        var maskImageBytes = maskMemStream.ToArray();
                        var maskImageString = Convert.ToBase64String(maskImageBytes);

                        maskImgContentTypeString = string.Format(Constants.ImageDataFormat, "image/png", maskImageString);

                        // Occasionally helpful for debugging masks
                        //var fileName = $"Mask-{DateTime.Now.Ticks}.png";
                        //maskMemStream.Seek(0, SeekOrigin.Begin);
                        //var uri = await _fileService.WriteImageFileToExternalStorageAsync(fileName, maskMemStream, true);
                    }
                }

                // Add the mask if it is empty or not so it can be cleared if there is no data
                parameters.Add(NavigationParams.MaskImgString, maskImgContentTypeString);

                var dispatcher = Shell.Current.CurrentPage.Dispatcher;
                await dispatcher?.DispatchAsync(async () =>
                {
                    await Shell.Current.GoToAsync("///MainPageTab", parameters);
                });
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
            var sameSizeMaskBitmap = maskBitmap.Resize(SourceBitmap.Info, SKFilterQuality.High);

            try
            {
                // Colorize the source bitmap using the mask, then create a black and white mask
                var colorizedBitmap = CreateMaskedBitmap(SourceBitmap, sameSizeMaskBitmap);
                var croppedBitmap = GetCroppedBitmap(colorizedBitmap, InitImgRectangle);

                using var croppedBitmapMemStream = new MemoryStream();
                using var croppedBitmapSkiaStream = new SKManagedWStream(croppedBitmapMemStream);

                croppedBitmap.Encode(croppedBitmapSkiaStream, SKEncodedImageFormat.Png, 100);
                croppedBitmapMemStream.Seek(0, SeekOrigin.Begin);
                var croppedBitmapImageBytes = croppedBitmapMemStream.ToArray();
                var croppedBitmapImageString = Convert.ToBase64String(croppedBitmapImageBytes);
                var croppedBitmapContentTypeString = string.Format(Constants.ImageDataFormat, "image/png", croppedBitmapImageString);

                var parameters = new Dictionary<string, object>
                {
                    { NavigationParams.ImageWidth, InitImgRectangleSize },
                    { NavigationParams.ImageHeight, InitImgRectangleSize },
                    { NavigationParams.InitImgString, croppedBitmapContentTypeString }
                };

                var croppedMaskContentTypeString = string.Empty;

                if (CanvasActions.Any(c => c.CanvasActionType == CanvasActionType.Mask))
                {
                    var blackAndWhiteMaskBitmap = CreateBlackAndWhiteMask(sameSizeMaskBitmap);
                    var croppedMask = GetCroppedBitmap(blackAndWhiteMaskBitmap, InitImgRectangle);

                    // Verify that the cropped mask contains anything useful
                    if (croppedMask.Pixels.Any(p => p.Alpha > 0))
                    {
                        using var croppedMaskMemStream = new MemoryStream();
                        using var croppedMaskSkiaStream = new SKManagedWStream(croppedMaskMemStream);

                        croppedMask.Encode(croppedMaskSkiaStream, SKEncodedImageFormat.Png, 100);
                        croppedMaskMemStream.Seek(0, SeekOrigin.Begin);
                        var croppedMaskImageBytes = croppedMaskMemStream.ToArray();
                        var croppedMaskImageString = Convert.ToBase64String(croppedMaskImageBytes);
                        croppedMaskContentTypeString = string.Format(Constants.ImageDataFormat, "image/png", croppedMaskImageString);
                    }
                }

                // Add the mask if it is empty or not so it can be cleared if there is no data
                parameters.Add(NavigationParams.MaskImgString, croppedMaskContentTypeString);

                var dispatcher = Shell.Current.CurrentPage.Dispatcher;
                await dispatcher?.DispatchAsync(async () =>
                {
                    await Shell.Current.GoToAsync("///MainPageTab", parameters);

                    await Toast.Make("Section has been cropped and set as source image.").Show();
                });
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

    [RelayCommand]
    private async Task DoSegmentation(SKPoint location)
    {
        if (_doingSegmentation)
        {
            return;
        }

        if (SourceBitmap == null)
        {
            await Shell.Current.DisplayAlert("No image", "There is no image on the canvas. Add an image and try again.", "OK");

            return;
        }

        if (!HasSegmentationImage)
        {
            if (SettingSegmentationImage)
            {
                await Shell.Current.DisplayAlert("Processing...", "The current image is still processing. Please try again.", "OK");
            }
            else
            {
                await Shell.Current.DisplayAlert("Problem", "There was a problem processing the current image. Please add an image and try again.", "OK");
            }

            return;
        }

        _doingSegmentation = true;
        IsBusy = true;

        var maskBitmap = await _segmentationService.DoSegmentation(location);

        if (maskBitmap != null)
        {
            if (SegmentationBitmap == null)
            {
                SegmentationBitmap = maskBitmap;
            }
            else
            {
                var newBitmap = new SKBitmap(SegmentationBitmap.Info);

                using (var combineCanvas = new SKCanvas(newBitmap))
                {
                    var paint = new SKPaint
                    {
                        BlendMode = SKBlendMode.SrcOver
                    };

                    combineCanvas.DrawBitmap(SegmentationBitmap, 0, 0, paint);

                    paint.BlendMode = SegmentationMode == SegmentationMode.AddArea ? SKBlendMode.SrcOver : SKBlendMode.DstOut;

                    combineCanvas.DrawBitmap(maskBitmap, 0, 0, paint);
                }

                SegmentationBitmap?.Dispose();
                SegmentationBitmap = null;

                SegmentationBitmap = newBitmap;
            }
        }
        
        IsBusy = false;
        _doingSegmentation = false;
    }

    [RelayCommand]
    private async Task ApplySegmentationMask()
    {
        if (SegmentationBitmap == null)
        {
            return;
        }

        IsBusy = true;

        var maskBitmap = await Task.Run(() =>
        {
            return CreateMaskBitmapFromSegmentationMask(SegmentationBitmap, CurrentColor.WithAlpha(CurrentAlpha));
        });

        var segmentationMask = new SegmentationMaskViewModel
        {
            CanvasActionType = CanvasActionType.Mask,
            Color = CurrentColor.WithAlpha(CurrentAlpha),
            Bitmap = maskBitmap
        };

        CanvasActions.Add(segmentationMask);

        MaskCanvasView?.InvalidateSurface();

        ClearSegmentationMask();

        IsBusy = false;
    }

    [RelayCommand]
    private void ClearSegmentationMask()
    {
        SegmentationBitmap?.Dispose();
        SegmentationBitmap = null;
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
            CanvasActions.Clear();

            var mask = await _fileService.GetMaskFileFromAppDataAsync(_sourceFileName);

            await dispatcher.DispatchAsync(() =>
            {
                if (mask?.Lines != null)
                {
                    foreach(var line in mask.Lines)
                    {
                        CanvasActions.Add(line);
                    }
                }
            });
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
        if (HapticFeedback.Default.IsSupported)
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }

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
    private void ChangeInitImgRectangleSize()
    {
        // Cycle through image rectangle sizes
        _imgRectIndex = (_imgRectIndex + 1) % _supportedImgRectSizes.Count;
        InitImgRectangleSize = _supportedImgRectSizes[_imgRectIndex];
    }

    [RelayCommand]
    private void ToggleInitImgRectangle()
    {
        ShowInitImgRectangle = !ShowInitImgRectangle;

        if (ShowInitImgRectangle)
        {
            ShowMaskLayer = true;
        }
    }

    [RelayCommand]
    private void ToggleMaskLayerVisibility()
    {
        ShowMaskLayer = !ShowMaskLayer;
    }

    [RelayCommand]
    private void SelectTool(IPaintingToolViewModel tool)
    {
        if (tool == null ||
            CurrentTool == tool)
        {
            return;
        }

        CurrentTool = tool;
    }

    unsafe private List<Color> ExtractColorPalette(SKBitmap bitmap, int targetNumber = 30)
    {
        if (bitmap == null)
        {
            return null;
        }

        const int maxSize = 128;

        var smallBitmap = _imageService.GetResizedSKBitmap(bitmap, maxSize, maxSize, false, false, false);

        SKColorType colorType = smallBitmap.ColorType;

        var width = smallBitmap.Width;
        var height = smallBitmap.Height;

        var allColors = new HashSet<Rgb>();

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

                if (colorType == SKColorType.Rgba8888)
                {
                    allColors.Add(new Rgb
                    {
                        R = byte1,
                        G = byte2,
                        B = byte3
                    });
                }
                else if (colorType == SKColorType.Bgra8888)
                {
                    allColors.Add(new Rgb
                    {
                        R = byte3,
                        G = byte2,
                        B = byte1
                    });
                }
            }
        }

        var colorSpaceComparison = new Cie1976Comparison();
        double minDeltaE = 10;

        var distinctRgbList = allColors.ToList();

        do
        {
            // Filter to distinct colors using an increasing Delta E each step
            distinctRgbList = filterColors(distinctRgbList, minDeltaE++);
        } while (distinctRgbList.Count > targetNumber);
        
        List<Rgb> filterColors(List<Rgb> sourceList, double minDeltaE)
        {
            var result = new List<Rgb>();

            foreach (var rgb in sourceList)
            {
                bool isDistinct = true;

                foreach (var existingRgb in result)
                {
                    if (rgb.Compare(existingRgb, colorSpaceComparison) < minDeltaE)
                    {
                        isDistinct = false;
                        break;
                    }
                }

                if (isDistinct)
                {
                    result.Add(rgb);
                }
            }

            return result;
        }

        distinctRgbList.Sort((firstColor, secondColor) =>
        {
            var step1 = ClusteredHueLumValueStep(firstColor, 8);
            var step2 = ClusteredHueLumValueStep(secondColor, 8);

            if (step1.h2 != step2.h2)
            {
                return step1.h2.CompareTo(step2.h2);
            }
            else if (step1.lum != step2.lum)
            {
                return step1.lum.CompareTo(step2.lum);
            }
            else
            {
                return step1.v2.CompareTo(step2.v2);
            }
        });

        var distinctColors = distinctRgbList.Select(rgb => new Color((byte)rgb.R, (byte)rgb.G, (byte)rgb.B));

        return distinctColors.ToList();
    }

    public static (int h2, double lum, int v2) ClusteredHueLumValueStep(Rgb color, int repetitions = 1)
    {
        double lumTest = Math.Sqrt(0.241 * color.R + 0.691 * color.G + 0.068 * color.B);

        var hsv = color.To<Hsv>();
        var hsl = hsv.To<Hsl>();
        var lum = hsl.L;

        int h2 = (int)(hsv.H * repetitions);
        int lum2 = (int)(lum * repetitions);
        int v2 = (int)(hsv.V * repetitions);

        // TODO - Reverse luminosity sorting to smooth color layout
        //if (h2 % 2 == 1)
        //{
        //    v2 = repetitions - v2;
        //    lum2 = repetitions - lum2;
        //}

        return (h2, lum2, v2);
    }

    public override Task OnNavigatedToAsync()
    {
        return base.OnNavigatedToAsync();
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

    private unsafe SKBitmap CreateMaskBitmapFromSegmentationMask(SKBitmap segmentationBitmap, Color maskColor)
    {
        if (segmentationBitmap == null || maskColor == null)
        {
            return null;
        }

        byte* srcPtr = (byte*)segmentationBitmap.GetPixels().ToPointer();

        var width = segmentationBitmap.Width;
        var height = segmentationBitmap.Height;

        SKColorType colorType = segmentationBitmap.ColorType;

        var resultBitmap = new SKBitmap(segmentationBitmap.Info.Width, segmentationBitmap.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

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

                if (srcByte4 != 0)
                {
                    *resultPtr++ = colorType == SKColorType.Rgba8888 ? maskColor.GetByteRed() : maskColor.GetByteBlue();
                    *resultPtr++ = maskColor.GetByteGreen();
                    *resultPtr++ = colorType == SKColorType.Rgba8888 ? maskColor.GetByteBlue() : maskColor.GetByteRed();
                    *resultPtr++ = byte.Max(1, maskColor.GetByteAlpha());
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

    private unsafe SKBitmap CreateMaskedBitmap(SKBitmap srcBitmap, SKBitmap maskBitmapOrig, bool randomizeMaskPixels = true)
    {
        if (srcBitmap == null ||
            maskBitmapOrig == null)
        {
            return null;
        }

        var maskBitmap = (maskBitmapOrig.Width == srcBitmap.Width && maskBitmapOrig.Height == srcBitmap.Height) ? maskBitmapOrig : maskBitmapOrig.Resize(srcBitmap.Info, SKFilterQuality.High);

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
                        const int rngAmount = 50;

                        var pos = _random.Next(0, 1) == 1;
                        mskByte1 = (byte)int.Clamp(((int)mskByte1) + (_random.Next(0, rngAmount) * (pos ? 1 : -1)), 0, 255);

                        pos = _random.Next(0, 1) == 1;
                        mskByte2 = (byte)int.Clamp(((int)mskByte2) + (_random.Next(0, rngAmount) * (pos ? 1 : -1)), 0, 255);
                        
                        pos = _random.Next(0, 1) == 1;
                        mskByte3 = (byte)int.Clamp(((int)mskByte3) + (_random.Next(0, rngAmount) * (pos ? 1 : -1)), 0, 255);
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

    partial void OnCurrentToolChanged(IPaintingToolViewModel value)
    {
        if (value == null)
        {
            return;
        }

        ShowContextMenu = value.Type == ToolType.PaintBucket;
    }
}
