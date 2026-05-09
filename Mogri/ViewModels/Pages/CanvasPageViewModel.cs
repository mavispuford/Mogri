using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Mogri.Enums;
using Mogri.Helpers;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;
using Mogri.Messages;
using SkiaSharp;
using System.Collections.ObjectModel;
using Mogri.Models;
using CommunityToolkit.Maui.Services;

namespace Mogri.ViewModels;

public partial class CanvasPageViewModel : PageViewModel, ICanvasPageViewModel
{
    private const double DefaultMaskToolAlpha = 0.5d;
    private const double DefaultTextToolAlpha = 1.0d;

    private enum CanvasResultTextHandling
    {
        Cancel,
        KeepEditable,
        ResolveText
    }

    private readonly object _setSegmentationImageLock = new();
    private readonly SemaphoreSlim _autoMaskSaveLock = new(1, 1);
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
    private int _setSegmentationImageVersion = 0;
    private double _maskToolAlpha = DefaultMaskToolAlpha;
    private double _textToolAlpha = DefaultTextToolAlpha;

    private CanvasUseMode _currentCanvasUseMode = CanvasUseMode.Inpaint;

    [ObservableProperty]
    public partial IRelayCommand? ResetZoomCommand { get; set; }

    [ObservableProperty]
    public partial List<IPaintingToolViewModel> AvailableTools { get; set; } = new();

    [ObservableProperty]
    public partial IPaintingToolViewModel CurrentTool { get; set; }

    [ObservableProperty]
    public partial double CurrentAlpha { get; set; } = DefaultMaskToolAlpha;

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
    public partial ObservableCollection<TextElementViewModel> TextElements { get; set; } = new();

    [ObservableProperty]
    public partial SKBitmap? SourceBitmap { get; set; }

    [ObservableProperty]
    public partial bool SegmentationAdd { get; set; } = true;

    [ObservableProperty]
    public partial SKBitmap? SegmentationBitmap { get; set; }


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
            Name = "Text",
            IconCode = "\uea1e",
            Effect = MaskEffect.None,
            Type = ToolType.Text,
            ContextButtons =
            [
                ContextButtonType.Alpha,
                ContextButtonType.ColorPicker
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

        WeakReferenceMessenger.Default.Register<AppStoppedMessage>(this, (r, m) =>
        {
            _ = ((CanvasPageViewModel)r).autoSaveOrDeleteMaskAsync();
        });
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

        if (lastAction is TextSnapshotCanvasActionViewModel textSnapshot)
        {
            CanvasActions.Remove(lastAction);
            restoreTextElements(textSnapshot.TextElementsSnapshot.Select(cloneTextElement));
            return;
        }

        if (lastAction is SnapshotCanvasActionViewModel snapshot)
        {
            CanvasActions.Remove(lastAction);

            var (bitmap, actions, textElements) = await _canvasHistoryService.RestoreSnapshotAsync(snapshot.SnapshotId);

            if (bitmap != null)
            {
                PreserveZoomOnNextBitmapChange = true;
                SourceBitmap = bitmap;
            }

            restoreTextElements(textElements);

            if (snapshot.IncludesCanvasActions)
            {
                restoreCanvasActions(actions);
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
        TextElements.Clear();
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
                // Latest-wins guard for SourceBitmap changes. A canceled older request can still finish later,
                // so only the newest version is allowed to publish its result.

                var magicWandTool = AvailableTools.FirstOrDefault(t => t.Type == ToolType.MagicWand);
                CancellationTokenSource? previousSetSegmentationImageCancellationTokenSource;
                CancellationTokenSource currentSetSegmentationImageCancellationTokenSource;
                int currentSetSegmentationImageVersion;

                lock (_setSegmentationImageLock)
                {
                    _setSegmentationImageRequestCount++;
                    _setSegmentationImageVersion++;
                    currentSetSegmentationImageVersion = _setSegmentationImageVersion;

                    // Swap in a new token source under the lock so the previous request can be canceled
                    // after the new request is fully registered as the current one.
                    previousSetSegmentationImageCancellationTokenSource = _setSegmentationImageCancellationTokenSource;
                    currentSetSegmentationImageCancellationTokenSource = new CancellationTokenSource();
                    _setSegmentationImageCancellationTokenSource = currentSetSegmentationImageCancellationTokenSource;

                    HasSegmentationImage = false;

                    if (magicWandTool != null)
                    {
                        magicWandTool.IsLoading = true;
                    }

                    SettingSegmentationImage = true;
                }

                if (previousSetSegmentationImageCancellationTokenSource != null)
                {
                    if (!previousSetSegmentationImageCancellationTokenSource.IsCancellationRequested)
                    {
                        previousSetSegmentationImageCancellationTokenSource.Cancel();
                    }

                    previousSetSegmentationImageCancellationTokenSource.Dispose();
                }

                var hasSegmentationImage = false;

                try
                {
                    hasSegmentationImage = await _segmentationService.SetImage(bitmap, currentSetSegmentationImageCancellationTokenSource.Token);
                }
                finally
                {
                    var shouldDisposeCurrentSetSegmentationImageCancellationTokenSource = false;

                    lock (_setSegmentationImageLock)
                    {
                        // A stale request may still complete after cancellation, but it must not change the
                        // UI flags if a newer SourceBitmap has already started processing.
                        if (currentSetSegmentationImageVersion == _setSegmentationImageVersion)
                        {
                            HasSegmentationImage = hasSegmentationImage;
                        }

                        _setSegmentationImageRequestCount--;

                        SettingSegmentationImage = _setSegmentationImageRequestCount > 0;

                        if (magicWandTool != null)
                        {
                            magicWandTool.IsLoading = _setSegmentationImageRequestCount > 0;
                        }

                        if (ReferenceEquals(_setSegmentationImageCancellationTokenSource, currentSetSegmentationImageCancellationTokenSource))
                        {
                            _setSegmentationImageCancellationTokenSource = null;
                            shouldDisposeCurrentSetSegmentationImageCancellationTokenSource = true;
                        }
                    }

                    if (shouldDisposeCurrentSetSegmentationImageCancellationTokenSource)
                    {
                        currentSetSegmentationImageCancellationTokenSource.Dispose();
                    }
                }

            }));
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        await saveImage();
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
        var hasMaskActions = CanvasActions != null
            && CanvasActions.Any(canvasAction => canvasAction.CanvasActionType == CanvasActionType.Mask);

        if (!hasMaskActions)
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

        var preparedSourceBitmap = result.PreparedSourceBitmap;
        var sourceBitmap = preparedSourceBitmap ?? SourceBitmap;
        if (sourceBitmap == null)
        {
            preparedSourceBitmap?.Dispose();
            IsBusy = false;
            return;
        }

        try
        {
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
                    await _fileService.WriteImageFileToExternalStorageAsync(fileName, memStream, false);

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
        }
        finally
        {
            preparedSourceBitmap?.Dispose();
        }

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

        var preparedSourceBitmap = result.PreparedSourceBitmap;
        var sourceBitmap = preparedSourceBitmap ?? SourceBitmap;

        if (sourceBitmap == null)
        {
            preparedSourceBitmap?.Dispose();
            IsBusy = false;
            return;
        }

        try
        {
            await Task.Run(async () =>
            {
                // Instead of using the Screen Capture (result.MaskBitmap) which might contain
                // UI-only artifacts or be at the wrong resolution/aspect ratio, we re-render the
                // layer internally using the SourceBitmap's dimensions. This ensures 1:1 alignment.
                using var rawMaskBitmap = GenerateRenderedLayer(sourceBitmap.Width, sourceBitmap.Height);

                var sameSizeMaskBitmap = rawMaskBitmap; // Already same size, no resize needed.

                try
                {
                    SKBitmap? colorizedBitmap;

                    if (_currentCanvasUseMode == CanvasUseMode.Inpaint || _currentCanvasUseMode == CanvasUseMode.PaintOnly)
                    {
                        colorizedBitmap = CreateMaskedBitmap(sourceBitmap, sameSizeMaskBitmap);
                    }
                    else
                    {
                        colorizedBitmap = sourceBitmap;
                    }

                    using var ownedColorizedBitmap = colorizedBitmap != null && !ReferenceEquals(colorizedBitmap, sourceBitmap)
                        ? colorizedBitmap
                        : null;

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
        }
        finally
        {
            preparedSourceBitmap?.Dispose();
        }

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

        var preparedSourceBitmap = result.PreparedSourceBitmap;
        var sourceBitmap = preparedSourceBitmap ?? SourceBitmap;
        if (sourceBitmap == null)
        {
            preparedSourceBitmap?.Dispose();
            IsBusy = false;
            return;
        }

        var snapshotId = await pushSnapshotAsync("Flatten", true);
        var transferredPreparedSourceBitmapOwnership = false;

        try
        {
            await Task.Run(async () =>
            {
                using var rawMaskBitmap = GenerateRenderedLayer(sourceBitmap.Width, sourceBitmap.Height);

                var hasMaskActions = CanvasActions.Any(canvasAction => canvasAction.CanvasActionType == CanvasActionType.Mask);
                var mergedBitmap = hasMaskActions
                    ? CreateMaskedBitmap(sourceBitmap, rawMaskBitmap)
                    : preparedSourceBitmap;

                if (mergedBitmap != null)
                {
                    var dispatcher = Shell.Current.CurrentPage?.Dispatcher;
                    await (dispatcher?.DispatchAsync(async () =>
                    {
                        var oldBitmap = SourceBitmap;
                        SourceBitmap = mergedBitmap;
                        oldBitmap?.Dispose();

                        CanvasActions.Clear();
                        TextElements.Clear();
                        ClearSegmentationMask();

                        if (ReferenceEquals(mergedBitmap, preparedSourceBitmap))
                        {
                            transferredPreparedSourceBitmapOwnership = true;
                        }

                        if (snapshotId != null)
                        {
                            insertSnapshotMarker(snapshotId, "Flatten", true);
                        }

                        await Toast.Make("Paint and Masks applied.").Show();
                    }) ?? Task.CompletedTask);
                }
            });
        }
        finally
        {
            if (!transferredPreparedSourceBitmapOwnership)
            {
                preparedSourceBitmap?.Dispose();
            }
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task FinishCroppingWithBoundingBox(CanvasCaptureResult result)
    {
        IsBusy = true;

        var preparedSourceBitmap = result.PreparedSourceBitmap;
        var sourceBitmap = preparedSourceBitmap ?? SourceBitmap;

        if (sourceBitmap == null)
        {
            preparedSourceBitmap?.Dispose();
            IsBusy = false;
            return;
        }

        try
        {
            await Task.Run(async () =>
            {
                // Re-render layer at SourceBitmap dimensions to ensure correct alignment of masks.
                // Using result.MaskBitmap (screen capture) dimensions causes scaling mismatches.
                using var rawMaskBitmap = GenerateRenderedLayer(sourceBitmap.Width, sourceBitmap.Height);

                var sameSizeMaskBitmap = rawMaskBitmap; // Already same size.

                try
                {
                    SKBitmap? colorizedBitmap;

                    if (_currentCanvasUseMode == CanvasUseMode.Inpaint || _currentCanvasUseMode == CanvasUseMode.PaintOnly)
                    {
                        colorizedBitmap = CreateMaskedBitmap(sourceBitmap, sameSizeMaskBitmap);
                    }
                    else
                    {
                        colorizedBitmap = sourceBitmap;
                    }

                    using var ownedColorizedBitmap = colorizedBitmap != null && !ReferenceEquals(colorizedBitmap, sourceBitmap)
                        ? colorizedBitmap
                        : null;

                    if (colorizedBitmap == null) return;

                    var croppedBitmap = GetCroppedBitmap(colorizedBitmap, BoundingBox);
                    using var ownedCroppedBitmap = croppedBitmap != null && !ReferenceEquals(croppedBitmap, colorizedBitmap)
                        ? croppedBitmap
                        : null;

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
                            using var ownedCroppedMask = croppedMask != null && !ReferenceEquals(croppedMask, blackAndWhiteMaskBitmap)
                                ? croppedMask
                                : null;

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
        }
        finally
        {
            preparedSourceBitmap?.Dispose();
        }

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

            using var fileStream = (await _fileService.OpenNormalizedPhotoStreamAsync(photo)).Stream;

            if (fileStream == null)
            {
                await _popupService.DisplayAlertAsync("Error", "Could not load the selected image.", "OK");
                return;
            }

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
                Order = getNextCanvasOrder(),
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

            void restorePersistedCanvasState()
            {
                if (mask == null)
                {
                    return;
                }

                var allActions = new List<CanvasActionViewModel>();

                if (mask.Lines != null)
                {
                    allActions.AddRange(mask.Lines);
                }

                if (mask.SegmentationMasks != null)
                {
                    allActions.AddRange(mask.SegmentationMasks);
                }

                restoreCanvasActions(allActions);
                restoreTextElements(mask.TextElements);
            }

            if (dispatcher != null)
            {
                await dispatcher.DispatchAsync(() =>
                {
                    restorePersistedCanvasState();
                });
            }
            else
            {
                restorePersistedCanvasState();
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
                { "TextElements", TextElements },
                { "OnSnapshotDelete", new Func<SnapshotCanvasActionViewModel, Task>(async snapshot => {
                    CanvasActions.Remove(snapshot);
                    var (bitmap, actions, textElements) = await _canvasHistoryService.RestoreSnapshotAsync(snapshot.SnapshotId);
                    if (bitmap != null)
                    {
                        PreserveZoomOnNextBitmapChange = true;
                        SourceBitmap = bitmap;
                    }
                    restoreTextElements(textElements);
                    if (snapshot.IncludesCanvasActions)
                    {
                        restoreCanvasActions(actions);
                    }
                }) },
                { "OnTextDelete", new Func<TextElementViewModel, Task>(textElement => {
                    DeleteTextCommand.Execute(textElement);
                    return Task.CompletedTask;
                }) },
                { "OnTextDuplicate", new Action<TextElementViewModel>(textElement => {
                    DuplicateTextCommand.Execute(textElement);
                }) },
                { "OnClearAll", new Func<Task>(async () => {
                    var firstSnapshot = CanvasActions.OfType<SnapshotCanvasActionViewModel>().FirstOrDefault();
                    if (firstSnapshot != null)
                    {
                        var (bitmap, _, _) = await _canvasHistoryService.RestoreSnapshotAsync(firstSnapshot.SnapshotId);
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

    public override async Task OnDisappearingAsync()
    {
        await autoSaveOrDeleteMaskAsync();
        await base.OnDisappearingAsync();
    }

    public override async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);

        if (query.TryGetValue(NavigationParams.CanvasImageString, out var canvasImageString) &&
            canvasImageString is string byteString)
        {
            var isBoundingBoxReturn = CurrentTool != null && CurrentTool.Type == ToolType.BoundingBox;
            var textHandling = await promptForCanvasResultTextHandlingAsync(isBoundingBoxReturn);

            if (textHandling == CanvasResultTextHandling.Cancel)
            {
                query.Clear();
                return;
            }

            if (isBoundingBoxReturn)
            {
                await BeginStitchingAsync(byteString, textHandling == CanvasResultTextHandling.ResolveText);
            }
            else
            {
                await ApplyCanvasResultAsync(byteString, textHandling == CanvasResultTextHandling.ResolveText);
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

    private async Task ApplyCanvasResultAsync(string byteString, bool resolveTextLayers)
    {
        using var stream = await _imageService.GetStreamFromContentTypeStringAsync(byteString, CancellationToken.None);
        var resultBitmap = SKBitmap.Decode(stream);

        if (resultBitmap == null)
        {
            return;
        }

        var snapshotId = await pushSnapshotAsync("To Canvas", false);
        var oldBitmap = SourceBitmap;

        PreserveZoomOnNextBitmapChange = true;
        SourceBitmap = resultBitmap;
        oldBitmap?.Dispose();

        if (resolveTextLayers)
        {
            TextElements.Clear();
        }

        if (snapshotId != null)
        {
            insertSnapshotMarker(snapshotId, "To Canvas", false);
        }
    }

    private async Task BeginStitchingAsync(string byteString, bool resolveTextLayers)
    {
        var snapshotId = await pushSnapshotAsync("Stitch", false);

        using var stream = await _imageService.GetStreamFromContentTypeStringAsync(byteString, CancellationToken.None);

        var stitchBitmap = SKBitmap.Decode(stream);
        if (stitchBitmap == null)
        {
            return;
        }

        var targetRect = getCanvasResultTargetRect();

        var finalBitmap = StitchBitmapIntoSource(SourceBitmap, stitchBitmap, BoundingBox);
        var oldBitmap = SourceBitmap;

        if (snapshotId != null)
        {
            insertSnapshotMarker(snapshotId, "Stitch", false);
        }

        PreserveZoomOnNextBitmapChange = true;
        SourceBitmap = finalBitmap;
        oldBitmap?.Dispose();

        if (resolveTextLayers)
        {
            removeTextElementsIntersecting(targetRect);
        }
    }

    private async Task<CanvasResultTextHandling> promptForCanvasResultTextHandlingAsync(bool isBoundingBoxReturn)
    {
        if (TextElements.Count == 0)
        {
            return CanvasResultTextHandling.KeepEditable;
        }

        var resolveTextOption = isBoundingBoxReturn
            ? "Apply Result and Remove Text in Area"
            : "Apply Result and Remove Text";

        var action = await _popupService.DisplayActionSheetAsync(
            "What should happen to text layers?",
            "Cancel",
            null,
            resolveTextOption,
            "Keep Text Editable");

        if (action == resolveTextOption)
        {
            return CanvasResultTextHandling.ResolveText;
        }

        if (action == "Keep Text Editable")
        {
            return CanvasResultTextHandling.KeepEditable;
        }

        return CanvasResultTextHandling.Cancel;
    }

    private SKRect getCanvasResultTargetRect()
    {
        var sourceBitmap = SourceBitmap;
        if (sourceBitmap == null
            || BoundingBoxScale <= 0d
            || BoundingBox.Width <= 0f
            || BoundingBox.Height <= 0f)
        {
            return sourceBitmap?.Info.Rect ?? SKRect.Empty;
        }

        return new SKRect(
            (float)(BoundingBox.Left * BoundingBoxScale),
            (float)(BoundingBox.Top * BoundingBoxScale),
            (float)(BoundingBox.Right * BoundingBoxScale),
            (float)(BoundingBox.Bottom * BoundingBoxScale));
    }

    private void removeTextElementsIntersecting(SKRect targetRect)
    {
        var overlappingTextElements = TextElements
            .Where(textElement => doRectsIntersect(TextElementLayoutHelper.GetAxisAlignedBounds(textElement), targetRect))
            .ToList();

        foreach (var textElement in overlappingTextElements)
        {
            TextElements.Remove(textElement);
        }
    }

    private static bool doRectsIntersect(SKRect left, SKRect right)
    {
        return !left.IsEmpty
            && !right.IsEmpty
            && left.Left < right.Right
            && left.Right > right.Left
            && left.Top < right.Bottom
            && left.Bottom > right.Top;
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

    /// <summary>
    /// Composites the rendered mask layer over the source bitmap using standard alpha blending.
    /// This delegates to SkiaSharp's canvas compositing (SrcOver) so that premultiplied alpha
    /// from GenerateRenderedLayer is handled correctly — matching how the canvas view renders
    /// mask strokes on screen.
    /// </summary>
    private SKBitmap? CreateMaskedBitmap(SKBitmap? srcBitmap, SKBitmap? maskBitmapOrig)
    {
        if (srcBitmap == null ||
            maskBitmapOrig == null)
        {
            return null;
        }

        var maskBitmap = (maskBitmapOrig.Width == srcBitmap.Width && maskBitmapOrig.Height == srcBitmap.Height)
            ? maskBitmapOrig
            : maskBitmapOrig.Resize(new SKImageInfo(srcBitmap.Width, srcBitmap.Height), new SKSamplingOptions(SKCubicResampler.Mitchell));

        if (maskBitmap == null) return null;

        var resultBitmap = new SKBitmap(srcBitmap.Width, srcBitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);

        using (var canvas = new SKCanvas(resultBitmap))
        {
            canvas.DrawBitmap(srcBitmap, 0, 0);
            canvas.DrawBitmap(maskBitmap, 0, 0);
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

        applyStoredAlphaForTool(value.Type);

        ShowContextMenu = value.Type == ToolType.MagicWand;
        OnPropertyChanged(nameof(IsZoomMode));
    }

    partial void OnCurrentAlphaChanged(double value)
    {
        switch (CurrentTool?.Type)
        {
            case ToolType.Text:
                _textToolAlpha = value;
                break;
            case ToolType.PaintBrush:
            case ToolType.MagicWand:
                _maskToolAlpha = value;
                break;
        }
    }

    private void applyStoredAlphaForTool(ToolType toolType)
    {
        if (!tryGetStoredAlphaForTool(toolType, out var storedAlpha)
            || Math.Abs(CurrentAlpha - storedAlpha) < double.Epsilon)
        {
            return;
        }

        CurrentAlpha = storedAlpha;
    }

    private bool tryGetStoredAlphaForTool(ToolType toolType, out double alpha)
    {
        switch (toolType)
        {
            case ToolType.Text:
                alpha = _textToolAlpha;
                return true;
            case ToolType.PaintBrush:
            case ToolType.MagicWand:
                alpha = _maskToolAlpha;
                return true;
            default:
                alpha = 0d;
                return false;
        }
    }

    private async Task<string?> pushSnapshotAsync(string description, bool includeCanvasActions)
    {
        var sourceBitmap = SourceBitmap;
        if (sourceBitmap == null) return null;

        var actionsToSave = includeCanvasActions ? CanvasActions.ToList() : null;
        var textElementsToSave = TextElements.Count > 0
            ? TextElements.Select(cloneTextElement).ToList()
            : null;
        var snapshotId = await _canvasHistoryService.SaveSnapshotAsync(sourceBitmap, actionsToSave, textElementsToSave);

        return snapshotId;
    }

    private void insertSnapshotMarker(string snapshotId, string description, bool includeCanvasActions)
    {
        CanvasActions.Add(new SnapshotCanvasActionViewModel
        {
            Order = getNextCanvasOrder(),
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

    [RelayCommand]
    private async Task AddText(SKPoint location)
    {
        var text = await _popupService.DisplayPromptAsync("Add Text", "Enter text or emoji:", placeholder: "Hello 👋");
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var nextOrder = PushTextSnapshot();

        TextElements.Add(new TextElementViewModel(nextOrder)
        {
            Text = text.Trim(),
            X = location.X,
            Y = location.Y,
            Color = CurrentColor,
            Alpha = (float)CurrentAlpha,
            Scale = 1f,
            Rotation = 0f
        });
    }

    [RelayCommand]
    private void DeleteText(TextElementViewModel element)
    {
        if (element == null || !TextElements.Contains(element))
        {
            return;
        }

        PushTextSnapshot();
        TextElements.Remove(element);
    }

    [RelayCommand]
    private void DuplicateText(TextElementViewModel element)
    {
        if (element == null || !TextElements.Contains(element))
        {
            return;
        }

        var nextOrder = PushTextSnapshot();

        TextElements.Add(new TextElementViewModel(Guid.NewGuid().ToString(), nextOrder, element.BaseFontSize)
        {
            Text = element.Text,
            X = element.X,
            Y = element.Y,
            Scale = element.Scale,
            Rotation = element.Rotation,
            Color = element.Color,
            Alpha = element.Alpha
        });
    }

    [RelayCommand]
    private async Task EditText(TextElementViewModel element)
    {
        if (element == null || !TextElements.Contains(element))
        {
            return;
        }

        var updatedText = await _popupService.DisplayPromptAsync(
            "Edit Text",
            "Update text or emoji:",
            placeholder: "Hello 👋",
            initialValue: element.Text);

        if (string.IsNullOrWhiteSpace(updatedText))
        {
            return;
        }

        element.Text = updatedText.Trim();
    }

    private int PushTextSnapshot()
    {
        var nextOrder = getNextCanvasOrder();
        CanvasActions.Add(new TextSnapshotCanvasActionViewModel
        {
            Order = nextOrder,
            TextElementsSnapshot = TextElements
                .Select(cloneTextElement)
                .ToList()
        });

        return nextOrder;
    }

    private int getNextCanvasOrder()
    {
        var nextCanvasActionOrder = CanvasActions.Count == 0 ? 0 : CanvasActions.Max(canvasAction => canvasAction.Order) + 1;
        var nextTextOrder = TextElements.Count == 0 ? 0 : checked((int)(TextElements.Max(textElement => textElement.Order) + 1));

        return Math.Max(nextCanvasActionOrder, nextTextOrder);
    }

    private static TextElementViewModel cloneTextElement(TextElementViewModel textElement)
    {
        return new TextElementViewModel(textElement.Id, textElement.Order, textElement.BaseFontSize)
        {
            Text = textElement.Text,
            X = textElement.X,
            Y = textElement.Y,
            Scale = textElement.Scale,
            Rotation = textElement.Rotation,
            Color = textElement.Color,
            Alpha = textElement.Alpha,
            IsSelected = textElement.IsSelected
        };
    }

    private void restoreCanvasActions(IEnumerable<CanvasActionViewModel>? actions)
    {
        CanvasActions.Clear();

        if (actions == null)
        {
            return;
        }

        foreach (var action in actions.OrderBy(action => action.Order))
        {
            CanvasActions.Add(action);
        }
    }

    private void restoreTextElements(IEnumerable<TextElementViewModel>? textElements)
    {
        TextElements.Clear();

        if (textElements == null)
        {
            return;
        }

        foreach (var textElement in textElements.OrderBy(textElement => textElement.Order))
        {
            TextElements.Add(textElement);
        }
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

    /// <summary>
    /// Persists canvas overlay state to disk when the source image is from the filesystem,
    /// or deletes the stale state file if no masks or text elements remain.
    /// </summary>
    private async Task autoSaveOrDeleteMaskAsync()
    {
        if (string.IsNullOrEmpty(_sourceFileName))
        {
            return;
        }

        if (!await _autoMaskSaveLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var maskActions = CanvasActions?.Where(ca => ca.CanvasActionType == CanvasActionType.Mask).ToList() ?? new();
            var textElements = TextElements.Select(cloneTextElement).ToList();

            if (maskActions.Count > 0 || textElements.Count > 0)
            {
                await _fileService.WriteMaskFileToAppDataAsync(_sourceFileName,
                    new MaskViewModel
                    {
                        Lines = maskActions.OfType<MaskLineViewModel>().ToList(),
                        SegmentationMasks = maskActions.OfType<SegmentationMaskViewModel>().ToList(),
                        TextElements = textElements
                    });
            }
            else
            {
                await _fileService.DeleteMaskFileFromAppDataAsync(_sourceFileName);
            }
        }
        finally
        {
            _autoMaskSaveLock.Release();
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