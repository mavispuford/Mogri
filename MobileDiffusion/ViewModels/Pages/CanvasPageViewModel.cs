using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Enums;
using MobileDiffusion.Helpers;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using SkiaSharp;
using System.Collections.ObjectModel;
using MobileDiffusion.ViewModels.CanvasContextButtons;
using MobileDiffusion.Models;

namespace MobileDiffusion.ViewModels;

public partial class CanvasPageViewModel : PageViewModel, ICanvasPageViewModel
{
    private readonly object _setSegmentationImageLock = new();
    private readonly IFileService _fileService;
    private readonly IImageService _imageService;
    private readonly IPopupService _popupService;
    private readonly ISegmentationService _segmentationService;
    private readonly IPatchService _patchService;

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
    public partial List<IPaintingToolViewModel> AvailableTools { get; set; } = new();

    [ObservableProperty]
    public partial IPaintingToolViewModel CurrentTool { get; set; }

    [ObservableProperty]
    public partial double CurrentAlpha { get; set; } = .5f;

    [ObservableProperty]
    public partial double CurrentBrushSize { get; set; } = 10d;

    [ObservableProperty]
    public partial Color CurrentColor { get; set; } = Colors.Black;

    [ObservableProperty]
    public partial Color PaletteIconColor { get; set; } = Colors.White;

    [ObservableProperty]
    public partial double BoundingBoxScale { get; set; }

    [ObservableProperty]
    public partial float BoundingBoxSize { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<CanvasActionViewModel> CanvasActions { get; set; } = new();

    [ObservableProperty]
    public partial SKBitmap SourceBitmap { get; set; }

    [ObservableProperty]
    public partial bool SegmentationAdd { get; set; } = true;

    [ObservableProperty]
    public partial SKBitmap SegmentationBitmap { get; set; }

    [ObservableProperty]
    public partial ImageSource SavedImageSource { get; set; }

    [ObservableProperty]
    public partial SKRect BoundingBox { get; set; }

    [ObservableProperty]
    public partial bool ShowMaskLayer { get; set; } = true;

    [ObservableProperty]
    public partial bool SettingSegmentationImage { get; set; } = false;

    [ObservableProperty]
    public partial bool HasSegmentationImage { get; set; } = false;

    [ObservableProperty]
    public partial bool ShowActions {get; set;}

    [ObservableProperty]
    public partial bool ShowContextMenu { get; set; } = false;

    [ObservableProperty]
    public partial bool GettingColorPalette { get; set; } = false;

    public bool IsZoomMode => CurrentTool?.Type == ToolType.Zoom;

    [ObservableProperty]
    private IAsyncRelayCommand _prepareForSavingCommand;

    public CanvasPageViewModel(
        IFileService fileService,
        IPopupService popupService,
        IImageService imageService,
        ISegmentationService segmentationService,
        IPatchService patchService,
        ILoadingService loadingService) : base(loadingService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _segmentationService = segmentationService ?? throw new ArgumentNullException(nameof(segmentationService));
        _patchService = patchService ?? throw new ArgumentNullException(nameof(patchService));

        Application.Current.Resources.TryGetValue("IndependenceAccent", out var independenceColor);

        if (independenceColor is Color paletteIconDarkColor)
        {
            _paletteIconDarkColor = paletteIconDarkColor;
        }

        BoundingBoxSize = _supportedImgRectSizes[_imgRectIndex];

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Brush",
            IconCode = "\ue3ae",
            Effect = MaskEffect.Paint,
            Type = ToolType.PaintBrush,
            ContextButtons = new List<CanvasContextButtonViewModel>
            {
                new BrushSizeContextButtonViewModel(this),
                new AlphaContextButtonViewModel(this),
                new ColorPickerContextButtonViewModel(this),
            }
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Eraser",
            IconCode = "\ue6d0",
            Effect = MaskEffect.Erase,
            Type = ToolType.Eraser,
            ContextButtons = new List<CanvasContextButtonViewModel>
            {
                new BrushSizeContextButtonViewModel(this)
            }
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Paint Bucket",
            IconCode = "\ue997",
            Effect = MaskEffect.Paint,
            Type = ToolType.PaintBucket,
            ContextButtons = new List<CanvasContextButtonViewModel>
            {
                new AlphaContextButtonViewModel(this),
                new ColorPickerContextButtonViewModel(this),
                new AddRemoveButtonViewModel(this),
            }
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Bounding Box",
            IconCode = "\ue3c6",
            Effect = MaskEffect.None,
            Type = ToolType.BoundingBox,
            ContextButtons = new List<CanvasContextButtonViewModel>
            {
                new BoundingBoxSizeContextButtonViewModel(this),
                new SnipContextButtonViewModel(this),
            }
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Eyedropper",
            IconCode = "\ue3b8",
            Effect = MaskEffect.None,
            Type = ToolType.Eyedropper,
            ContextButtons = 
            [
                new ColorPickerContextButtonViewModel(this),
            ]
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Zoom",
            IconCode = "\ue8ff",
            Effect = MaskEffect.None,
            Type = ToolType.Zoom,
            ContextButtons = []
        });

        // Placeholder for gesture paint bucket - Might just be added into the base control
        //AvailableTools.Add(new PaintingToolViewModel
        //{
        //    Name = "Paint Bucket",
        //    IconCode = "\ue155",
        //    Effect = MaskEffect.Paint,
        //    Type = ToolType.PaintBucket
        //});

        CurrentTool = AvailableTools.FirstOrDefault();
    }

    partial void OnCurrentColorChanged(Color value)
    {
        if (value != null)
        {
            PaletteIconColor = value.GetLuminosity() > .8 ? _paletteIconDarkColor : Colors.White;
        }
    }

    [RelayCommand]
    private void Undo()
    {
        if (CanvasActions == null || !CanvasActions.Any())
        {
            return;
        }

        CanvasActions.Remove(CanvasActions.Last());
    }

    [RelayCommand]
    private async Task Clear()
    {
        var result = await Shell.Current.DisplayAlertAsync("Clear mask?", "Are you sure you would like to clear the mask?", "YES", "Cancel");

        if (!result)
        {
            return;
        }

        if (CanvasActions == null || !CanvasActions.Any())
        {
            return;
        }

        CanvasActions.Clear();
    }

    partial void OnSourceBitmapChanged(SKBitmap value)
    {
        if (value != null)
        {
            _ = Task.WhenAll(Task.Run(() =>
            {

                GettingColorPalette = true;

                _colorPalette = _imageService.ExtractColorPalette(value, 48);

                GettingColorPalette = false;
            }), Task.Run(async () =>
            {
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

            }));
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
            selection = await Shell.Current.DisplayActionSheetAsync("Save?", "Cancel", null, maskChoice, imageChoice);
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
        if (SourceBitmap == null)
        {
            await Toast.Make("There is no image to save.").Show();

            return;
        }

        await PrepareForSavingCommand?.ExecuteAsync(FinishSavingCommand);
    }

    [RelayCommand]
    private async Task SendToImageToImage()
    {
        if (SourceBitmap == null)
        {
            await Toast.Make("There is no image to send.").Show();

            return;
        }

        await PrepareForSavingCommand?.ExecuteAsync(FinishSendingToImageToImageCommand);
    }

    [RelayCommand]
    private async Task BeginCropImageRect()
    {
        if (SourceBitmap == null)
        {
            await Toast.Make("There is no image data to crop.").Show();

            return;
        }

        await PrepareForSavingCommand?.ExecuteAsync(FinishCroppingWithBoundingBoxCommand);
    }

    [RelayCommand]
    private async Task FinishSaving(CanvasCaptureResult result)
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
    private async Task FinishSendingToImageToImage(CanvasCaptureResult result)
    {
        IsBusy = true;

        await Task.Run(async () =>
        {
            var maskBitmap = result.MaskBitmap;
            var sameSizeMaskBitmap = maskBitmap.Resize(SourceBitmap.Info, new SKSamplingOptions(SKCubicResampler.Mitchell));

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

                var thumbnailString = _imageService.GetThumbnailString(colorizedBitmap, "image/png");

                var parameters = new Dictionary<string, object>
                {
                    { NavigationParams.ImageWidth, constrainedDimensions.Width },
                    { NavigationParams.ImageHeight, constrainedDimensions.Height },
                    { NavigationParams.InitImgString, colorizedImgContentTypeString },
                    { NavigationParams.InitImgThumbnail, thumbnailString }
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
    private async Task FinishCroppingWithBoundingBox(CanvasCaptureResult result)
    {
        IsBusy = true;

        await Task.Run(async () =>
        {
            var maskBitmap = result.MaskBitmap;
            var sameSizeMaskBitmap = maskBitmap.Resize(SourceBitmap.Info, new SKSamplingOptions(SKCubicResampler.Mitchell));

            try
            {
                // Colorize the source bitmap using the mask, then create a black and white mask
                var colorizedBitmap = CreateMaskedBitmap(SourceBitmap, sameSizeMaskBitmap);
                var croppedBitmap = GetCroppedBitmap(colorizedBitmap, BoundingBox);

                using var croppedBitmapMemStream = new MemoryStream();
                using var croppedBitmapSkiaStream = new SKManagedWStream(croppedBitmapMemStream);

                croppedBitmap.Encode(croppedBitmapSkiaStream, SKEncodedImageFormat.Png, 100);
                croppedBitmapMemStream.Seek(0, SeekOrigin.Begin);
                var croppedBitmapImageBytes = croppedBitmapMemStream.ToArray();
                var croppedBitmapImageString = Convert.ToBase64String(croppedBitmapImageBytes);
                var croppedBitmapContentTypeString = string.Format(Constants.ImageDataFormat, "image/png", croppedBitmapImageString);

                var thumbnailString = _imageService.GetThumbnailString(croppedBitmap, "image/png");

                var parameters = new Dictionary<string, object>
                {
                    { NavigationParams.ImageWidth, BoundingBoxSize },
                    { NavigationParams.ImageHeight, BoundingBoxSize },
                    { NavigationParams.InitImgString, croppedBitmapContentTypeString },
                    { NavigationParams.InitImgThumbnail, thumbnailString }
                };

                var croppedMaskContentTypeString = string.Empty;

                if (CanvasActions.Any(c => c.CanvasActionType == CanvasActionType.Mask))
                {
                    var blackAndWhiteMaskBitmap = CreateBlackAndWhiteMask(sameSizeMaskBitmap);
                    var croppedMask = GetCroppedBitmap(blackAndWhiteMaskBitmap, BoundingBox);

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
        try
        {
            var fileResult = await MediaPicker.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 });
            var photo = fileResult?.FirstOrDefault();

            if (photo == null)
            {
                return;
            }

            using var fileStream = await photo.OpenReadAsync();

            await LoadSourceBitmapUsingStream(fileStream, photo.FileName);
        }
        catch (Exception)
        {
            await Toast.Make("Unable to load image. Please check permissions and try again.").Show();
        }
    }

    [RelayCommand]
    private async Task DoSegmentation(SKPoint[] points)
    {
        if (_doingSegmentation ||
            points == null ||
            points.Length == 0)
        {
            return;
        }

        if (SourceBitmap == null)
        {
            await Shell.Current.DisplayAlertAsync("No image", "There is no image on the canvas. Add an image and try again.", "OK");

            return;
        }

        if (!HasSegmentationImage)
        {
            if (SettingSegmentationImage)
            {
                await Shell.Current.DisplayAlertAsync("Processing...", "The current image is still processing. Please try again.", "OK");
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Problem", "There was a problem processing the current image. Please add an image and try again.", "OK");
            }

            return;
        }

        try
        {
            _doingSegmentation = true;
            IsBusy = true;

            var maskBitmap = await _segmentationService.DoSegmentation(points);
            
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

                        paint.BlendMode = SegmentationAdd ? SKBlendMode.SrcOver : SKBlendMode.DstOut;

                        combineCanvas.DrawBitmap(maskBitmap, 0, 0, paint);
                    }

                    SegmentationBitmap?.Dispose();
                    SegmentationBitmap = null;

                    SegmentationBitmap = newBitmap;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await Toast.Make("Failed to perform segmentation.").Show();
        }
        finally
        {
            IsBusy = false;
            _doingSegmentation = false;
        }
    }

    [RelayCommand]
    private async Task ApplySegmentationMask()
    {
        if (SegmentationBitmap == null)
        {
            return;
        }

        try
        {
            IsBusy = true;

            var maskBitmap = await Task.Run(() =>
            {
                return CreateMaskBitmapFromSegmentationMask(SegmentationBitmap);
            });

            var segmentationMask = new SegmentationMaskViewModel
            {
                CanvasActionType = CanvasActionType.Mask,
                Color = CurrentColor.WithAlpha((float)CurrentAlpha),
                Bitmap = maskBitmap
            };

            CanvasActions.Add(segmentationMask);

            ClearSegmentationMask();
        }
        catch (Exception ex)
        {
             Console.WriteLine(ex.Message);
             await Toast.Make("Failed to apply mask.").Show();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearSegmentationMask()
    {
        SegmentationBitmap?.Dispose();
        SegmentationBitmap = null;
        _segmentationService.Reset();
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
                    var canvasActions = new ObservableCollection<CanvasActionViewModel>();

                    foreach(var line in mask.Lines)
                    {
                        canvasActions.Add(line);
                    }

                    CanvasActions = canvasActions;
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
        try
        {
            if (HapticFeedback.Default.IsSupported)
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            }

            var parameters = new Dictionary<string, object> {
                { NavigationParams.Color, CurrentColor },
                { NavigationParams.ColorPalette, _colorPalette },
            };

            var color = await _popupService.ShowPopupForResultAsync("ColorPickerPopup", parameters) as Color;

            if (color != null)
            {
                CurrentColor = color;
            }
        }
        catch (Exception)
        {
            await Toast.Make("Unable to show color picker.").Show();
        }
    }

    [RelayCommand]
    private void ChangeBoundingBoxSize()
    {
        // Cycle through image rectangle sizes
        _imgRectIndex = (_imgRectIndex + 1) % _supportedImgRectSizes.Count;
        BoundingBoxSize = _supportedImgRectSizes[_imgRectIndex];
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
            if (CurrentTool != null && CurrentTool.Type == ToolType.BoundingBox)
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

        var finalBitmap = StitchBitmapIntoSource(SourceBitmap, stitchBitmap, BoundingBox);

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

                // TODO - Make this configurable per SD service, since they accept different masks
                // Also, mask expectations can change even in the same SD repo
                if (mskByte4 != 0)
                {
                    // Pure black
                    *resultPtr++ = 0;
                    *resultPtr++ = 0;
                    *resultPtr++ = 0;
                    *resultPtr++ = 255;

                    // White with transparency
                    //*resultPtr++ = 255;
                    //*resultPtr++ = 255;
                    //*resultPtr++ = 255;
                    //*resultPtr++ = mskByte4;

                    // Shades of grey with no transparency, based on the alpha
                    //*resultPtr++ = mskByte4;
                    //*resultPtr++ = mskByte4;
                    //*resultPtr++ = mskByte4;
                    //*resultPtr++ = 255;
                }
                else
                {
                    // Fully transparent
                    //*resultPtr++ = 0;
                    //*resultPtr++ = 0;
                    //*resultPtr++ = 0;
                    //*resultPtr++ = 0;

                    // Pure black
                    //*resultPtr++ = 0;
                    //*resultPtr++ = 0;
                    //*resultPtr++ = 0;
                    //*resultPtr++ = 255;

                    // White with transparency
                    *resultPtr++ = 255;
                    *resultPtr++ = 255;
                    *resultPtr++ = 255;
                    *resultPtr++ = mskByte4;
                }

            }
        }

        return resultBitmap;
    }

    private SKBitmap CreateMaskBitmapFromSegmentationMask(SKBitmap segmentationBitmap)
    {
        if (segmentationBitmap == null)
        {
            return null;
        }

        var resultBitmap = new SKBitmap(segmentationBitmap.Info.Width, segmentationBitmap.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        using (var canvas = new SKCanvas(resultBitmap))
        {
            canvas.Clear(SKColors.Transparent);

            using (var paint = new SKPaint())
            {
                paint.ColorFilter = SKColorFilter.CreateBlendMode(SKColors.White, SKBlendMode.SrcIn);
                canvas.DrawBitmap(segmentationBitmap, 0, 0, paint);
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

        var maskBitmap = (maskBitmapOrig.Width == srcBitmap.Width && maskBitmapOrig.Height == srcBitmap.Height) ? maskBitmapOrig : maskBitmapOrig.Resize(srcBitmap.Info, new SKSamplingOptions(SKCubicResampler.Mitchell));

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

                        var pos = _random.Next(0, 2) == 1;
                        mskByte1 = (byte)int.Clamp(((int)mskByte1) + (_random.Next(0, rngAmount) * (pos ? 1 : -1)), 0, 255);

                        pos = _random.Next(0, 2) == 1;
                        mskByte2 = (byte)int.Clamp(((int)mskByte2) + (_random.Next(0, rngAmount) * (pos ? 1 : -1)), 0, 255);
                        
                        pos = _random.Next(0, 2) == 1;
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
                    //float strength = Math.Min(mskByte4, (byte)204) / 255f;

                    float strength = mskByte4 / 255f;

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

        var left = (float)(cropRect.Left * BoundingBoxScale);
        var top = (float)(cropRect.Top * BoundingBoxScale);

        var adjustedRect = new SKRect(
            left, 
            top,
            left + BoundingBoxSize, 
            top + BoundingBoxSize);
        
        var info = new SKImageInfo
        {
            AlphaType = SKAlphaType.Unpremul,
            ColorSpace = bitmap.ColorSpace,
            ColorType = bitmap.ColorType,
            Height = (int)BoundingBoxSize,
            Width = (int)BoundingBoxSize,
        };

        var croppedBitmap = new SKBitmap(info);

        var source = new SKRect(adjustedRect.Left, adjustedRect.Top,
                                   adjustedRect.Right, adjustedRect.Bottom);
        var dest = new SKRect(0, 0, adjustedRect.Width, adjustedRect.Height);

        using (var canvas = new SKCanvas(croppedBitmap))
        {
            var paint = new SKPaint()
            {
                IsAntialias = false,
            };

            using var image = SKImage.FromBitmap(bitmap);
            canvas.DrawImage(image, source, dest, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), paint);
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
                (float)(rect.Left * BoundingBoxScale),
                (float)(rect.Top * BoundingBoxScale),
                (float)(rect.Right * BoundingBoxScale),
                (float)(rect.Bottom * BoundingBoxScale));
        }

        var source = new SKRect(0, 0, adjustedRect.Width, adjustedRect.Height);
        var dest = new SKRect(adjustedRect.Left, adjustedRect.Top, adjustedRect.Right, adjustedRect.Bottom);

        using (var canvas = new SKCanvas(resultBitmap))
        {
            var paint = new SKPaint
            {
                IsAntialias = true,
            };

            canvas.DrawBitmap(bitmap, 0, 0);

            // Scale it if the size doesn't match the current init image rectangle
            var toStitch = bitmapToStitchIn.Width != dest.Width || bitmapToStitchIn.Height != dest.Height ?
                bitmapToStitchIn.Resize(adjustedRect.Size.ToSizeI(), new SKSamplingOptions(SKCubicResampler.Mitchell)) :
                bitmapToStitchIn;

            using var image = SKImage.FromBitmap(toStitch);
            canvas.DrawImage(image, source, dest, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
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
        OnPropertyChanged(nameof(IsZoomMode));
    }

    [RelayCommand]
    private async Task PatchAsync()
    {
        if (SourceBitmap == null || CanvasActions.Count == 0)
        {
            await _popupService.DisplayAlertAsync("Info", "Nothing to patch!", "OK");
            return;
        }

        var action = await _popupService.DisplayActionSheetAsync("Inpaint / Patch", "Cancel", null, "Use Last Mask Only", "Use All Masks");
        
        if (action == "Cancel" || string.IsNullOrEmpty(action))
            return;

        bool useLastOnly = action == "Use Last Mask Only";

        try
        {
            IsBusy = true;
            await Task.Delay(100);

            // Unload Segmentation Service to free resource
            _segmentationService.UnloadModel();

            using var mask = await Task.Run(() => GenerateMask(useLastOnly));

            var result = await _patchService.PatchImageAsync(SourceBitmap, mask);
            
            // Unload Patch Service after use
            _patchService.UnloadModel();

            if (result != null)
            {
                SourceBitmap = result;
            }
        }
        catch (Exception ex)
        {
            await _popupService.DisplayAlertAsync("Error", $"Inpainting failed: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleSegmentationAdd()
    {
        SegmentationAdd = !SegmentationAdd;

        _segmentationService.Reset();
    }

    private SKBitmap GenerateMask(bool useLastOnly)
    {
        if (SourceBitmap == null) return null;

        var mask = new SKBitmap(SourceBitmap.Width, SourceBitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(mask);
        
        // Background is black (keep original)
        canvas.Clear(SKColors.Black);

        // Filter actions
        var actionsToRender = useLastOnly 
            ? CanvasActions.Where(x => x == CanvasActions.LastOrDefault()).ToList() 
            : CanvasActions.ToList();

        // Draw masks (White = Inpaint)
        foreach (var action in actionsToRender)
        {
            if (action is MaskLineViewModel line)
            {
                using var paint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = line.BrushSize,
                    StrokeCap = SKStrokeCap.Round,
                    StrokeJoin = SKStrokeJoin.Round,
                    IsAntialias = true
                };

                if (line.MaskEffect == MaskEffect.Paint)
                {
                    paint.BlendMode = SKBlendMode.SrcOver; 
                    paint.Color = SKColors.White;
                }
                else // Erase
                {
                    paint.BlendMode = SKBlendMode.Src;
                    paint.Color = SKColors.Black;
                }

                if (line.Path != null && line.Path.Count > 0)
                {
                    using var path = new SKPath();
                    path.MoveTo(line.Path[0]);
                    for (var i = 1; i < line.Path.Count; i++)
                    {
                        path.ConicTo(line.Path[i - 1], line.Path[i], .5f);
                    }
                    canvas.DrawPath(path, paint);
                }
            }
            else if (action is SegmentationMaskViewModel seg && seg.Bitmap != null)
            {
                 using var paint = new SKPaint();
                 // Create filter to make non-transparent pixels white
                 paint.ColorFilter = SKColorFilter.CreateBlendMode(SKColors.White, SKBlendMode.SrcIn); 
                 
                 canvas.DrawBitmap(seg.Bitmap, new SKRect(0, 0, mask.Width, mask.Height), paint);
            }
        }
        
        return mask;
    }

    [RelayCommand]
    private void ToggleActionsVisibility()
    {
        ShowActions = !ShowActions;
    }

    public override bool OnBackButtonPressed()
    {
        return true;
    }
}
