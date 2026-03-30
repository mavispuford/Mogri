using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Enums;
using Mogri.Helpers;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;
using SkiaSharp;
using System.Collections.ObjectModel;
using Mogri.Models;
using CommunityToolkit.Maui.Services;

namespace Mogri.ViewModels;

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
    private string? _sourceFileName;
    private Random _random = new Random();
    private bool _doingSegmentation = false;
    private CancellationTokenSource? _setSegmentationImageCancellationTokenSource;
    private int _setSegmentationImageRequestCount = 0;

    private enum CanvasUseMode
    {
        Inpaint,
        PaintOnly,
        MaskOnly,
        ImageOnly
    }

    private CanvasUseMode _currentCanvasUseMode = CanvasUseMode.Inpaint;

    [ObservableProperty]
    public partial IRelayCommand? ResetZoomCommand { get; set; }

    [ObservableProperty]
    public partial List<IPaintingToolViewModel> AvailableTools { get; set; } = new();

    [ObservableProperty]
    public partial IPaintingToolViewModel CurrentTool { get; set; }

    [ObservableProperty]
    public partial double CurrentAlpha { get; set; } = .5f;

    [ObservableProperty]
    public partial double CurrentBrushSize { get; set; } = 10d;

    [ObservableProperty]
    public partial double CurrentNoise { get; set; } = .5d;

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
    public partial SKBitmap? SourceBitmap { get; set; }

    [ObservableProperty]
    public partial bool SegmentationAdd { get; set; } = true;

    [ObservableProperty]
    public partial SKBitmap? SegmentationBitmap { get; set; }

    [ObservableProperty]
    public partial ImageSource? SavedImageSource { get; set; }

    [ObservableProperty]
    public partial SKRect BoundingBox { get; set; }

    [ObservableProperty]
    public partial bool ShowMaskLayer { get; set; } = true;

    [ObservableProperty]
    public partial bool SettingSegmentationImage { get; set; } = false;

    [ObservableProperty]
    public partial bool HasSegmentationImage { get; set; } = false;

    [ObservableProperty]
    public partial bool ShowActions { get; set; }

    [ObservableProperty]
    public partial bool ShowContextMenu { get; set; } = false;

    [ObservableProperty]
    public partial bool GettingColorPalette { get; set; } = false;

    public bool IsZoomMode => CurrentTool?.Type == ToolType.Zoom;

    [ObservableProperty]
    public partial bool PreserveZoomOnNextBitmapChange { get; set; }

    [ObservableProperty]
    private IAsyncRelayCommand? _prepareForSavingCommand;

    private readonly ICanvasHistoryService _canvasHistoryService;

    public CanvasPageViewModel(
        IFileService fileService,
        IPopupService popupService,
        IImageService imageService,
        ISegmentationService segmentationService,
        IPatchService patchService,
        ICanvasHistoryService canvasHistoryService,
        ILoadingService loadingService) : base(loadingService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _segmentationService = segmentationService ?? throw new ArgumentNullException(nameof(segmentationService));
        _patchService = patchService ?? throw new ArgumentNullException(nameof(patchService));
        _canvasHistoryService = canvasHistoryService ?? throw new ArgumentNullException(nameof(canvasHistoryService));

        // Precompute the noise bitmap on a background thread so the first stroke is smooth
        NoiseShaderHelper.Initialize();

        if (Application.Current?.Resources.TryGetValue("Cadet", out var independenceColor) == true &&
            independenceColor is Color paletteIconDarkColor)
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
            ContextButtons =
            [
                ContextButtonType.BrushSize,
                ContextButtonType.Alpha,
                ContextButtonType.ColorPicker,
                ContextButtonType.Noise
            ]
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Eraser",
            IconCode = "\ue6d0",
            Effect = MaskEffect.Erase,
            Type = ToolType.Eraser,
            ContextButtons =
            [
                ContextButtonType.BrushSize
            ]
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Magic Wand",
            IconImagePath = "wand_stars.png",
            Effect = MaskEffect.Paint,
            Type = ToolType.MagicWand,
            ContextButtons =
            [
                ContextButtonType.Alpha,
                ContextButtonType.ColorPicker,
                ContextButtonType.AddRemove,
                ContextButtonType.Noise
            ]
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Bounding Box",
            IconCode = "\ue3c6",
            Effect = MaskEffect.None,
            Type = ToolType.BoundingBox,
            ContextButtons =
            [
                ContextButtonType.BoundingBoxSize
            ]
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Eyedropper",
            IconCode = "\ue3b8",
            Effect = MaskEffect.None,
            Type = ToolType.Eyedropper,
            ContextButtons =
            [
                ContextButtonType.ColorPicker
            ]
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Zoom",
            IconCode = "\ue8ff",
            Effect = MaskEffect.None,
            Type = ToolType.Zoom,
            ContextButtons =
            [
                ContextButtonType.ResetZoom
            ]
        });

        CurrentTool = AvailableTools.FirstOrDefault()!;

        SourceBitmap = new SKBitmap(768, 1024);
        using (var canvas = new SKCanvas(SourceBitmap))
        {
            canvas.Clear(SKColors.WhiteSmoke);
        }
    }

    partial void OnCurrentColorChanged(Color value)
    {
        if (value != null)
        {
            PaletteIconColor = value.GetLuminosity() > .8 ? _paletteIconDarkColor : Colors.White;
        }
    }

    [RelayCommand]
    private async Task Undo()
    {
        if (CanvasActions == null || !CanvasActions.Any())
        {
            return;
        }

        var lastAction = CanvasActions.Last();

        if (lastAction is SnapshotCanvasActionViewModel snapshot)
        {
            CanvasActions.Remove(lastAction);

            var (bitmap, actions) = await _canvasHistoryService.RestoreSnapshotAsync(snapshot.SnapshotId);

            if (bitmap != null)
            {
                PreserveZoomOnNextBitmapChange = true;
                SourceBitmap = bitmap;
            }

            if (snapshot.IncludesCanvasActions && actions != null)
            {
                CanvasActions.Clear();
                foreach (var action in actions)
                {
                    CanvasActions.Add(action);
                }
            }
        }
        else
        {
            CanvasActions.Remove(lastAction);
        }
    }

    private async Task clearAllActionsAndHistoryAsync()
    {
        await _canvasHistoryService.ClearAllAsync();
        CanvasActions.Clear();
    }

    [RelayCommand]
    private async Task Clear()
    {
        var result = await _popupService.DisplayAlertAsync("Clear masks?", "Are you sure you would like to clear the masks?", "YES", "Cancel");

        if (!result)
        {
            return;
        }

        if (CanvasActions == null || !CanvasActions.Any())
        {
            return;
        }

        var actionsToRemove = CanvasActions.Where(a => a is not SnapshotCanvasActionViewModel).ToList();
        foreach (var a in actionsToRemove)
        {
            CanvasActions.Remove(a);
        }
    }

    partial void OnSourceBitmapChanged(SKBitmap? value)
    {
        if (value != null)
        {
            // Capture for lambda safety
            var bitmap = value;

            _ = Task.WhenAll(Task.Run(() =>
            {
                GettingColorPalette = true;

                var palette = _imageService.ExtractColorPalette(bitmap, 48);
                if (palette != null)
                {
                    _colorPalette = palette;
                }

                GettingColorPalette = false;
            }), Task.Run(async () =>
            {
                var magicWandTool = AvailableTools.FirstOrDefault(t => t.Type == ToolType.MagicWand);

                if (magicWandTool != null)
                {
                    lock (_setSegmentationImageLock)
                    {
                        _setSegmentationImageRequestCount++;

                        magicWandTool.IsLoading = true;
                        SettingSegmentationImage = true;
                    }
                }

                if (_setSegmentationImageCancellationTokenSource != null && !_setSegmentationImageCancellationTokenSource.IsCancellationRequested)
                {
                    _setSegmentationImageCancellationTokenSource.Cancel();
                }

                _setSegmentationImageCancellationTokenSource = new CancellationTokenSource();

                HasSegmentationImage = await _segmentationService.SetImage(bitmap, _setSegmentationImageCancellationTokenSource?.Token ?? CancellationToken.None);

                if (magicWandTool != null)
                {
                    lock (_setSegmentationImageLock)
                    {
                        _setSegmentationImageRequestCount--;

                        magicWandTool.IsLoading = _setSegmentationImageRequestCount > 0;
                        SettingSegmentationImage = _setSegmentationImageRequestCount > 0;
                    }
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

        var dispatcher = Shell.Current.CurrentPage?.Dispatcher;

        if (dispatcher != null)
        {
            await dispatcher.DispatchAsync(async () =>
            {
                selection = await _popupService.DisplayActionSheetAsync("Save?", "Cancel", null, maskChoice, imageChoice);
            });
        }
        else
        {
            selection = await _popupService.DisplayActionSheetAsync("Save?", "Cancel", null, maskChoice, imageChoice);
        }

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

        if (CanvasActions == null)
        {
            return;
        }

        try
        {
            var maskActions = CanvasActions.Where(ca => ca.CanvasActionType == CanvasActionType.Mask).ToList();

            for (var i = 0; i < maskActions.Count; i++)
            {
                maskActions[i].Order = i;
            }

            var maskUri = await _fileService.WriteMaskFileToAppDataAsync(_sourceFileName,
                new MaskViewModel
                {
                    Lines = maskActions.OfType<MaskLineViewModel>().ToList(),
                    SegmentationMasks = maskActions.OfType<SegmentationMaskViewModel>().ToList()
                });

            await Toast.Make("Mask saved.").Show();
        }
        catch (Exception)
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

        await (PrepareForSavingCommand?.ExecuteAsync(FinishSavingCommand) ?? Task.CompletedTask);
    }

    private async Task<bool> AskUseCanvasMode()
    {
        if (CanvasActions == null || !CanvasActions.Any())
        {
            _currentCanvasUseMode = CanvasUseMode.ImageOnly;
            return true;
        }

        const string inpaint = "Paint and Mask (inpainting)";
        const string paintOnly = "Paint only (NO mask)";
        const string maskOnly = "Mask only (NO Paint)";
        const string imageOnly = "Image only";

        var selection = await _popupService.DisplayActionSheetAsync("Image Mode", "Cancel", null, inpaint, paintOnly, maskOnly, imageOnly);

        if (selection == inpaint) _currentCanvasUseMode = CanvasUseMode.Inpaint;
        else if (selection == paintOnly) _currentCanvasUseMode = CanvasUseMode.PaintOnly;
        else if (selection == maskOnly) _currentCanvasUseMode = CanvasUseMode.MaskOnly;
        else if (selection == imageOnly) _currentCanvasUseMode = CanvasUseMode.ImageOnly;
        else return false;

        return true;
    }

    [RelayCommand]
    private async Task SendToImageToImage()
    {
        ShowActions = false;

        if (SourceBitmap == null)
        {
            await Toast.Make("There is no image to send.").Show();

            return;
        }

        if (!await AskUseCanvasMode())
        {
            return;
        }

        await (PrepareForSavingCommand?.ExecuteAsync(FinishSendingToImageToImageCommand) ?? Task.CompletedTask);
    }

    [RelayCommand]
    private async Task BeginCropImageRect()
    {
        ShowActions = false;

        if (SourceBitmap == null)
        {
            await Toast.Make("There is no image data to crop.").Show();

            return;
        }

        if (!await AskUseCanvasMode())
        {
            return;
        }

        await (PrepareForSavingCommand?.ExecuteAsync(FinishCroppingWithBoundingBoxCommand) ?? Task.CompletedTask);
    }

    [RelayCommand]
    private async Task FinishSaving(CanvasCaptureResult result)
    {
        IsBusy = true;

        var sourceBitmap = SourceBitmap;
        if (sourceBitmap == null)
        {
            IsBusy = false;
            return;
        }

        await Task.Run(async () =>
        {
            var dispatcher = Shell.Current.CurrentPage.Dispatcher;

            try
            {
                using var memStream = new MemoryStream();
                using var skiaStream = new SKManagedWStream(memStream);

                sourceBitmap.Encode(skiaStream, SKEncodedImageFormat.Png, 100);

                var fileName = $"CanvasImage-{DateTime.Now.Ticks}.png";
                memStream.Seek(0, SeekOrigin.Begin);
                var uri = await _fileService.WriteImageFileToExternalStorageAsync(fileName, memStream, false);

                await (dispatcher?.DispatchAsync(async () =>
                {
                    await Toast.Make($"{fileName} saved.").Show();
                }) ?? Task.CompletedTask);
            }
            catch (Exception)
            {
                await (dispatcher?.DispatchAsync(async () =>
                {
                    await Toast.Make("Failed to save image. Please try again.").Show();
                }) ?? Task.CompletedTask);
            }
        });

        IsBusy = false;
    }

    private SKBitmap GenerateRenderedLayer(int width, int height)
    {
        var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.Transparent);

            // Render all actions with isSaving=true to ensure High Fidelity (Noise/Color) 
            // instead of UI Fallbacks (Hatch Patterns).
            var info = new SKImageInfo(width, height);

            if (CanvasActions != null)
            {
                foreach (var action in CanvasActions)
                {
                    action.Execute(canvas, info, true);
                }
            }
        }
        return bmp;
    }

    [RelayCommand]
    private async Task FinishSendingToImageToImage(CanvasCaptureResult result)
    {
        IsBusy = true;

        var sourceBitmap = SourceBitmap;

        if (sourceBitmap == null)
        {
            IsBusy = false;
            return;
        }

        await Task.Run(async () =>
        {
            // Instead of using the Screen Capture (result.MaskBitmap) which might contain
            // UI-only artifacts or be at the wrong resolution/aspect ratio, we re-render the
            // layer internally using the SourceBitmap's dimensions. This ensures 1:1 alignment.
            using var rawMaskBitmap = GenerateRenderedLayer(sourceBitmap.Width, sourceBitmap.Height);

            var sameSizeMaskBitmap = rawMaskBitmap; // Already same size, no resize needed.

            try
            {
                // Colorize the source bitmap using the mask, then create a black and white mask
                SKBitmap? colorizedBitmap;

                if (_currentCanvasUseMode == CanvasUseMode.Inpaint || _currentCanvasUseMode == CanvasUseMode.PaintOnly)
                {
                    // Update: Pass opaque=false because our mask layer uses alpha modulation
                    colorizedBitmap = CreateMaskedBitmap(sourceBitmap, sameSizeMaskBitmap);
                }
                else
                {
                    colorizedBitmap = sourceBitmap;
                }

                if (colorizedBitmap == null)
                {
                    return;
                }

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

                var parameters = new Dictionary<string, object?>
                {
                    { NavigationParams.ImageWidth, constrainedDimensions.Width },
                    { NavigationParams.ImageHeight, constrainedDimensions.Height },
                    { NavigationParams.InitImgString, colorizedImgContentTypeString },
                    { NavigationParams.InitImgThumbnail, thumbnailString }
                };

                var maskImgContentTypeString = string.Empty;

                if ((_currentCanvasUseMode == CanvasUseMode.Inpaint || _currentCanvasUseMode == CanvasUseMode.MaskOnly) &&
                    CanvasActions != null &&
                    CanvasActions.Any(c => c.CanvasActionType == CanvasActionType.Mask))
                {
                    // Check if any visible mask exists
                    if (rawMaskBitmap.Pixels.Any(p => p.Alpha > 0))
                    {
                        using var blackAndWhiteMaskBitmap = CreateBlackAndWhiteMask(sameSizeMaskBitmap);
                        if (blackAndWhiteMaskBitmap != null)
                        {
                            using var maskMemStream = new MemoryStream();
                            using var maskSkiaStream = new SKManagedWStream(maskMemStream);
                            blackAndWhiteMaskBitmap.Encode(maskSkiaStream, SKEncodedImageFormat.Png, 100);
                            maskMemStream.Seek(0, SeekOrigin.Begin);
                            var maskImageBytes = maskMemStream.ToArray();
                            var maskImageString = Convert.ToBase64String(maskImageBytes);

                            maskImgContentTypeString = string.Format(Constants.ImageDataFormat, "image/png", maskImageString);
                        }
                    }
                }


                // Add the mask if it is empty or not so it can be cleared if there is no data
                parameters.Add(NavigationParams.MaskImgString, maskImgContentTypeString);

                var dispatcher = Shell.Current.CurrentPage?.Dispatcher;
                await (dispatcher?.DispatchAsync(async () =>
                {
                    await Shell.Current.GoToAsync("///MainPageTab", parameters);
                }) ?? Task.CompletedTask);
            }
            catch
            {
                // Ignored
            }
        });

        IsBusy = false;
    }

    [RelayCommand]
    private async Task ApplyPaintAndMasks()
    {
        ShowActions = false;

        if (SourceBitmap == null)
        {
            await Toast.Make("There is no image to apply paint/masks to.").Show();
            return;
        }

        var result = await _popupService.DisplayAlertAsync("Flatten Canvas?",
            "This will apply the paint/masks and replace the current canvas image. This can be undone from the Canvas History.\n\nContinue?",
            "YES",
            "NO");

        if (!result)
        {
            return;
        }

        await (PrepareForSavingCommand?.ExecuteAsync(FinishApplyingPaintAndMasksCommand) ?? Task.CompletedTask);
    }

    [RelayCommand]
    private async Task FinishApplyingPaintAndMasks(CanvasCaptureResult result)
    {
        IsBusy = true;

        var sourceBitmap = SourceBitmap;
        if (sourceBitmap == null)
        {
            IsBusy = false;
            return;
        }

        var snapshotId = await pushSnapshotAsync("Flatten", true);

        await Task.Run(async () =>
        {
            using var rawMaskBitmap = GenerateRenderedLayer(sourceBitmap.Width, sourceBitmap.Height);

            var mergedBitmap = CreateMaskedBitmap(sourceBitmap, rawMaskBitmap);

            if (mergedBitmap != null)
            {
                var dispatcher = Shell.Current.CurrentPage?.Dispatcher;
                await (dispatcher?.DispatchAsync(async () =>
                {
                    var oldBitmap = SourceBitmap;
                    SourceBitmap = mergedBitmap;
                    oldBitmap?.Dispose();

                    CanvasActions.Clear();
                    ClearSegmentationMask();

                    if (snapshotId != null)
                    {
                        insertSnapshotMarker(snapshotId, "Flatten", true);
                    }

                    await Toast.Make("Paint and Masks applied.").Show();
                }) ?? Task.CompletedTask);
            }
        });

        IsBusy = false;
    }

    [RelayCommand]
    private async Task FinishCroppingWithBoundingBox(CanvasCaptureResult result)
    {
        IsBusy = true;

        var sourceBitmap = SourceBitmap;

        if (sourceBitmap == null)
        {
            IsBusy = false;
            return;
        }

        await Task.Run(async () =>
        {
            // Re-render layer at SourceBitmap dimensions to ensure correct alignment of masks.
            // Using result.MaskBitmap (screen capture) dimensions causes scaling mismatches.
            using var rawMaskBitmap = GenerateRenderedLayer(sourceBitmap.Width, sourceBitmap.Height);

            var sameSizeMaskBitmap = rawMaskBitmap; // Already same size.

            try
            {
                // Colorize the source bitmap using the mask, then create a black and white mask
                SKBitmap? colorizedBitmap;

                if (_currentCanvasUseMode == CanvasUseMode.Inpaint || _currentCanvasUseMode == CanvasUseMode.PaintOnly)
                {
                    colorizedBitmap = CreateMaskedBitmap(sourceBitmap, sameSizeMaskBitmap);
                }
                else
                {
                    colorizedBitmap = sourceBitmap;
                }

                if (colorizedBitmap == null) return;

                var croppedBitmap = GetCroppedBitmap(colorizedBitmap, BoundingBox);

                if (croppedBitmap == null) return;

                using var croppedBitmapMemStream = new MemoryStream();
                using var croppedBitmapSkiaStream = new SKManagedWStream(croppedBitmapMemStream);

                croppedBitmap.Encode(croppedBitmapSkiaStream, SKEncodedImageFormat.Png, 100);
                croppedBitmapMemStream.Seek(0, SeekOrigin.Begin);
                var croppedBitmapImageBytes = croppedBitmapMemStream.ToArray();
                var croppedBitmapImageString = Convert.ToBase64String(croppedBitmapImageBytes);
                var croppedBitmapContentTypeString = string.Format(Constants.ImageDataFormat, "image/png", croppedBitmapImageString);

                var thumbnailString = _imageService.GetThumbnailString(croppedBitmap, "image/png");

                var parameters = new Dictionary<string, object?>
                {
                    { NavigationParams.ImageWidth, BoundingBoxSize },
                    { NavigationParams.ImageHeight, BoundingBoxSize },
                    { NavigationParams.InitImgString, croppedBitmapContentTypeString },
                    { NavigationParams.InitImgThumbnail, thumbnailString }
                };

                var croppedMaskContentTypeString = string.Empty;

                if ((_currentCanvasUseMode == CanvasUseMode.Inpaint || _currentCanvasUseMode == CanvasUseMode.MaskOnly) &&
                    CanvasActions.Any(c => c.CanvasActionType == CanvasActionType.Mask))
                {
                    using var blackAndWhiteMaskBitmap = CreateBlackAndWhiteMask(sameSizeMaskBitmap);
                    if (blackAndWhiteMaskBitmap != null)
                    {
                        var croppedMask = GetCroppedBitmap(blackAndWhiteMaskBitmap, BoundingBox);

                        if (croppedMask != null)
                        {
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
                    }
                }

                // Add the mask if it is empty or not so it can be cleared if there is no data
                parameters.Add(NavigationParams.MaskImgString, croppedMaskContentTypeString);

                var dispatcher = Shell.Current.CurrentPage?.Dispatcher;
                await (dispatcher?.DispatchAsync(async () =>
                {
                    await Shell.Current.GoToAsync("///MainPageTab", parameters);

                    await Toast.Make("Section has been cropped and set as source image.").Show();
                }) ?? Task.CompletedTask);
            }
            catch
            {
                // Ignored
            }
        });

        IsBusy = false;
    }

    [RelayCommand]
    private async Task ShowMediaPicker()
    {
        try
        {
            var photo = await _popupService.PickSinglePhotoAsync();
            
            if (photo == null)
            {
                return;
            }

            const string newCanvasWithImageOption = "New Canvas with Image";
            const string scaleImageToExistingCanvasOption = "Scale Image to Existing Canvas";
            const string scaleImageToBoundingBoxOption = "Scale Image to Bounding Box";

            var actions = new List<string> { newCanvasWithImageOption, scaleImageToExistingCanvasOption };

            if (CurrentTool?.Type == ToolType.BoundingBox)
            {
                actions.Add(scaleImageToBoundingBoxOption);
            }

            var action = await _popupService.DisplayActionSheetAsync("Set Image", "Cancel", null, actions.ToArray());

            if (action == "Cancel" || action == null)
            {
                return;
            }

            using var fileStream = await photo.OpenReadAsync();

            if (action == newCanvasWithImageOption)
            {
                await _canvasHistoryService.ClearAllAsync();
                ClearSegmentationMask();
                await LoadSourceBitmapUsingStream(fileStream, photo.FileName);
            }
            else if (action == scaleImageToExistingCanvasOption)
            {
                try
                {
                    IsBusy = true;

                    var loadedBitmap = LoadBitmapFromStream(fileStream);

                    if (loadedBitmap != null && SourceBitmap != null)
                    {
                        var info = new SKImageInfo(SourceBitmap.Width, SourceBitmap.Height);
                        var resizedBitmap = loadedBitmap.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));

                        loadedBitmap.Dispose();

                        SourceBitmap = resizedBitmap;
                        _sourceFileName = null;

                        ClearSegmentationMask();
                        await clearAllActionsAndHistoryAsync();
                    }
                }
                finally
                {
                    IsBusy = false;
                }
            }
            else if (action == scaleImageToBoundingBoxOption)
            {
                try
                {
                    IsBusy = true;

                    var snapshotId = await pushSnapshotAsync("Insert Image", false);

                    var loadedBitmap = LoadBitmapFromStream(fileStream);

                    if (loadedBitmap != null)
                    {
                        var stitchedBitmap = StitchBitmapIntoSource(SourceBitmap, loadedBitmap, BoundingBox);

                        loadedBitmap.Dispose();

                        if (snapshotId != null)
                        {
                            insertSnapshotMarker(snapshotId, "Insert Image", false);
                        }

                        SourceBitmap = stitchedBitmap;
                    }
                }
                finally
                {
                    IsBusy = false;
                }
            }
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
            await _popupService.DisplayAlertAsync("No image", "There is no image on the canvas. Add an image and try again.", "OK");

            return;
        }

        if (!HasSegmentationImage)
        {
            if (SettingSegmentationImage)
            {
                await _popupService.DisplayAlertAsync("Processing...", "The current image is still processing. Please try again.", "OK");
            }
            else
            {
                await _popupService.DisplayAlertAsync("Problem", "There was a problem processing the current image. Please add an image and try again.", "OK");
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
    private async Task InvertSegmentationMask()
    {
        if (SegmentationBitmap == null)
        {
            if (SourceBitmap == null) return;
            
            // If no existing segmentation mask, the inverted state is simply a completely filled mask
            var fullMask = new SKBitmap(SourceBitmap.Info);
            using var canvas = new SKCanvas(fullMask);
            canvas.Clear(_segmentationService.MaskColor);
            
            SegmentationBitmap = fullMask;
            return;
        }

        try
        {
            IsBusy = true;

            var invertedBitmap = await Task.Run(() =>
            {
                return _segmentationService.InvertMask(SegmentationBitmap);
            });

            var oldBitmap = SegmentationBitmap;
            SegmentationBitmap = invertedBitmap;
            oldBitmap?.Dispose();

            // Reset SAM state so subsequent taps start fresh instead of building on the pre-inverted state
            _segmentationService.Reset();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await Toast.Make("Failed to invert mask.").Show();
        }
        finally
        {
            IsBusy = false;
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
                Color = CurrentColor,
                Alpha = (float)CurrentAlpha,
                Noise = CurrentNoise,
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

    private SKBitmap? LoadBitmapFromStream(Stream? stream)
    {
        if (stream == null) return null;

        var codec = SKCodec.Create(stream);
        if (codec == null) return null;

        var info = new SKImageInfo
        {
            AlphaType = SKAlphaType.Unpremul,
            ColorSpace = codec.Info.ColorSpace,
            ColorType = codec.Info.ColorType,
            Height = codec.Info.Height,
            Width = codec.Info.Width,
        };

        return SKBitmap.Decode(codec, info);
    }

    private async Task LoadSourceBitmapUsingStream(Stream? stream, string fileName)
    {
        try
        {
            IsBusy = true;

            // Instead of a simple SKBitmap.Decode() call, we're using a codec and SKImageInfo with Unpremul for the
            // AlphaType so masked images can be reopened after being created

            var sourceBitmap = LoadBitmapFromStream(stream);

            // Wrap in dispatch call because ApplyQueryAttributes can call this method and it
            // appears to be called from a non-UI thread.
            var dispatcher = Dispatcher.GetForCurrentThread();
            if (dispatcher != null)
            {
                await dispatcher.DispatchAsync(() =>
                {
                    SourceBitmap = sourceBitmap;
                });
            }
            else
            {
                SourceBitmap = sourceBitmap;
            }

            _sourceFileName = fileName;
            await clearAllActionsAndHistoryAsync();

            var mask = await _fileService.GetMaskFileFromAppDataAsync(_sourceFileName);

            if (dispatcher != null)
            {
                await dispatcher.DispatchAsync(() =>
                {
                    if (mask != null)
                    {
                        var allActions = new List<CanvasActionViewModel>();

                        if (mask.Lines != null)
                        {
                            allActions.AddRange(mask.Lines);
                        }

                        if (mask.SegmentationMasks != null)
                        {
                            allActions.AddRange(mask.SegmentationMasks);
                        }

                        CanvasActions = new ObservableCollection<CanvasActionViewModel>(allActions.OrderBy(a => a.Order));
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await _popupService.DisplayAlertAsync("Error", "Failed to load mask data", "OK");
        }
        finally
        {
            if (stream != null)
                await stream.DisposeAsync();

            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ShowHistory()
    {
        try
        {
            var parameters = new Dictionary<string, object> {
                { "Actions", CanvasActions },
                { "OnSnapshotDelete", new Func<SnapshotCanvasActionViewModel, Task>(async snapshot => {
                    CanvasActions.Remove(snapshot);
                    var (bitmap, actions) = await _canvasHistoryService.RestoreSnapshotAsync(snapshot.SnapshotId);
                    if (bitmap != null)
                    {
                        PreserveZoomOnNextBitmapChange = true;
                        SourceBitmap = bitmap;
                    }
                    if (snapshot.IncludesCanvasActions && actions != null)
                    {
                        CanvasActions.Clear();
                        foreach (var action in actions)
                        {
                            CanvasActions.Add(action);
                        }
                    }
                }) },
                { "OnClearAll", new Func<Task>(async () => {
                    var firstSnapshot = CanvasActions.OfType<SnapshotCanvasActionViewModel>().FirstOrDefault();
                    if (firstSnapshot != null)
                    {
                        var (bitmap, _) = await _canvasHistoryService.RestoreSnapshotAsync(firstSnapshot.SnapshotId);
                        if (bitmap != null)
                        {
                            PreserveZoomOnNextBitmapChange = true;
                            SourceBitmap = bitmap;
                        }
                    }
                    await clearAllActionsAndHistoryAsync();
                }) }
            };

            await _popupService.ShowPopupAsync("CanvasHistoryPopup", parameters);
        }
        catch (Exception ex)
        {
            await _popupService.DisplayAlertAsync("Error", $"Unable to open history popup: {ex.Message}", "OK");
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
        var snapshotId = await pushSnapshotAsync("Stitch", false);

        var tokenSource = new CancellationTokenSource();

        using var stream = await _imageService.GetStreamFromContentTypeStringAsync(byteString, tokenSource.Token);

        var stitchBitmap = SKBitmap.Decode(stream);

        var finalBitmap = StitchBitmapIntoSource(SourceBitmap, stitchBitmap, BoundingBox);

        if (snapshotId != null)
        {
            insertSnapshotMarker(snapshotId, "Stitch", false);
        }

        SourceBitmap = finalBitmap;
    }

    private unsafe SKBitmap? CreateBlackAndWhiteMask(SKBitmap? maskBitmap)
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

                // Check generic mask alpha. 
                // Note: Mask logic may need configuration per SD service backend.
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

    private SKBitmap? CreateMaskBitmapFromSegmentationMask(SKBitmap? segmentationBitmap)
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

    private unsafe SKBitmap? CreateMaskedBitmap(SKBitmap? srcBitmap, SKBitmap? maskBitmapOrig, bool randomizeMaskPixels = true)
    {
        if (srcBitmap == null ||
            maskBitmapOrig == null)
        {
            return null;
        }

        var maskBitmap = (maskBitmapOrig.Width == srcBitmap.Width && maskBitmapOrig.Height == srcBitmap.Height) ? maskBitmapOrig : maskBitmapOrig.Resize(srcBitmap.Info, new SKSamplingOptions(SKCubicResampler.Mitchell));

        if (maskBitmap == null) return null;

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

                    // Mask color is derived directly from the mask bitmap. Noise is now applied when drawing
                    // so we don't add per-pixel CPU-based noise here.

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

                    // Some server implementations expect a near-zero alpha; keep example here for reference
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

    private SKBitmap? GetCroppedBitmap(SKBitmap? bitmap, SKRect cropRect)
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

    private SKBitmap? StitchBitmapIntoSource(SKBitmap? bitmap, SKBitmap? bitmapToStitchIn, SKRect rect)
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

            if (toStitch != null)
            {
                using var image = SKImage.FromBitmap(toStitch);
                canvas.DrawImage(image, source, dest, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
            }
        }

        return resultBitmap;
    }

    partial void OnCurrentToolChanged(IPaintingToolViewModel value)
    {
        if (value == null)
        {
            return;
        }

        ShowContextMenu = value.Type == ToolType.MagicWand;
        OnPropertyChanged(nameof(IsZoomMode));
    }

    private async Task<string?> pushSnapshotAsync(string description, bool includeCanvasActions)
    {
        var sourceBitmap = SourceBitmap;
        if (sourceBitmap == null) return null;

        var actionsToSave = includeCanvasActions ? CanvasActions.ToList() : null;
        var snapshotId = await _canvasHistoryService.SaveSnapshotAsync(sourceBitmap, actionsToSave);

        return snapshotId;
    }

    private void insertSnapshotMarker(string snapshotId, string description, bool includeCanvasActions)
    {
        CanvasActions.Add(new SnapshotCanvasActionViewModel
        {
            SnapshotId = snapshotId,
            Description = description,
            IncludesCanvasActions = includeCanvasActions
        });
    }

    [RelayCommand]
    private async Task PatchAsync()
    {
        ShowActions = false;

        var sourceBitmap = SourceBitmap;
        if (sourceBitmap == null || CanvasActions.Count == 0)
        {
            await _popupService.DisplayAlertAsync("Info", "Nothing to patch!", "OK");
            return;
        }

        bool useLastOnly = false;
        var maskCount = CanvasActions.Count(ca => ca.CanvasActionType == CanvasActionType.Mask);

        if (maskCount > 1)
        {
            const string useLastMaskOnlyOption = "Use Last Mask Only";
            const string useAllMasksOption = "Use All Masks";

            var action = await _popupService.DisplayActionSheetAsync("Patch", "Cancel", null, useLastMaskOnlyOption, useAllMasksOption);

            if (action == "Cancel" || string.IsNullOrEmpty(action))
                return;

            useLastOnly = action == useLastMaskOnlyOption;
        }

        var snapshotId = await pushSnapshotAsync("Patch", false);

        try
        {
            IsBusy = true;
            await Task.Delay(100);

            while (SettingSegmentationImage)
            {
                await Task.Delay(100);
            }

            // Unload Segmentation Service to free resource
            _segmentationService.UnloadModel();

            using var mask = await Task.Run(() => GenerateMask(useLastOnly));

            if (mask != null)
            {
                var result = await _patchService.PatchImageAsync(sourceBitmap, mask);

                if (result != null)
                {
                    if (snapshotId != null)
                    {
                        insertSnapshotMarker(snapshotId, "Patch", false);
                    }
                    PreserveZoomOnNextBitmapChange = true;
                    SourceBitmap = result;
                }
            }

            // Unload Patch Service after use
            _patchService.UnloadModel();
        }
        catch (Exception ex)
        {
            await _popupService.DisplayAlertAsync("Error", $"Patching failed: {ex.Message}", "OK");
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

    private SKBitmap? GenerateMask(bool useLastOnly)
    {
        var sourceBitmap = SourceBitmap;
        if (sourceBitmap == null) return null;

        var mask = new SKBitmap(sourceBitmap.Width, sourceBitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(mask);

        // Background is black (keep original)
        canvas.Clear(SKColors.Black);

        if (CanvasActions != null)
        {
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
        }

        return mask;
    }

    [RelayCommand]
    private void ToggleActionsVisibility()
    {
        ShowActions = !ShowActions;

        vibrate(HapticFeedbackType.Click);
    }

    public override bool OnBackButtonPressed()
    {
        if (CurrentTool is { Type: ToolType.Zoom })
        {
            ResetZoomCommand?.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    [RelayCommand]
    private async Task SetResolution()
    {
        var sourceBitmap = SourceBitmap;
        if (sourceBitmap == null)
            return;

        var action = await _popupService.DisplayActionSheetAsync("Set Resolution", "Cancel", null, "Create a blank canvas", "Scale Existing Canvas");

        if (action == "Create a blank canvas")
        {
            var parameters = new Dictionary<string, object>
            {
                { NavigationParams.Width, (double)sourceBitmap.Width },
                { NavigationParams.Height, (double)sourceBitmap.Height },
                { NavigationParams.InitImgString, string.Empty }
            };

            var result = await _popupService.ShowPopupForResultAsync("ResolutionSelectPopup", parameters) as IDictionary<string, object>;

            if (result != null)
            {
                if (result.TryGetValue(NavigationParams.Width, out var widthParam) &&
                    double.TryParse(widthParam.ToString(), out var width) &&
                    result.TryGetValue(NavigationParams.Height, out var heightParam) &&
                    double.TryParse(heightParam.ToString(), out var height))
                {
                    ClearSegmentationMask();
                    await clearAllActionsAndHistoryAsync();

                    SourceBitmap?.Dispose();
                    SourceBitmap = new SKBitmap((int)width, (int)height);
                    using (var canvas = new SKCanvas(SourceBitmap))
                    {
                        canvas.Clear(SKColors.WhiteSmoke);
                    }
                    OnPropertyChanged(nameof(SourceBitmap));
                }
            }
        }
        else if (action == "Scale Existing Canvas")
        {
            var parameters = new Dictionary<string, object>
            {
                { NavigationParams.Width, (double)sourceBitmap.Width },
                { NavigationParams.Height, (double)sourceBitmap.Height },
                { NavigationParams.InitImgString, string.Empty }
            };

            var result = await _popupService.ShowPopupForResultAsync("ResolutionSelectPopup", parameters) as IDictionary<string, object>;

            if (result != null)
            {
                if (result.TryGetValue(NavigationParams.Width, out var widthParam) &&
                    double.TryParse(widthParam.ToString(), out var width) &&
                    result.TryGetValue(NavigationParams.Height, out var heightParam) &&
                    double.TryParse(heightParam.ToString(), out var height))
                {
                    ClearSegmentationMask();
                    await clearAllActionsAndHistoryAsync();

                    var resized = sourceBitmap.Resize(new SKImageInfo((int)width, (int)height), new SKSamplingOptions(SKCubicResampler.Mitchell));
                    SourceBitmap?.Dispose();
                    SourceBitmap = resized;
                    OnPropertyChanged(nameof(SourceBitmap));
                }
            }
        }
    }

    private void vibrate(HapticFeedbackType type)
    {
        if (HapticFeedback.Default.IsSupported)
        {
            HapticFeedback.Default.Perform(type);
        }
    }
}
