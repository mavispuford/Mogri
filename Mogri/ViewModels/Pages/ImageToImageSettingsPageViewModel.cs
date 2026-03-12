using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Helpers;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;
using Mogri.Models;
using SkiaSharp.Views.Maui.Controls;

namespace Mogri.ViewModels;

public partial class ImageToImageSettingsPageViewModel : PageViewModel, IImageToImageSettingsPageViewModel
{
    private readonly IImageService _imageService;
    private readonly IPopupService _popupService;

    private PromptSettings? _settings;

    private CancellationTokenSource? _initCancellationTokenSource;

    private CancellationTokenSource? _maskCancellationTokenSource;

    [ObservableProperty]
    public partial bool FitImageServerSide { get; set; }

    [ObservableProperty]
    public partial bool FitImageClientSide { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingInitImage { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMaskImage { get; set; }

    [ObservableProperty]
    public partial string? Strength { get; set; }

    [ObservableProperty]
    public partial string? StrengthPlaceholder { get; set; }

    [ObservableProperty]
    public partial string? MaskBlur { get; set; }

    [ObservableProperty]
    public partial string? MaskBlurPlaceholder { get; set; }

    [ObservableProperty]
    public partial ImageSource? InitImageSource { get; set; }

    [ObservableProperty]
    public partial ImageSource? MaskImageSource { get; set; }

    public ImageToImageSettingsPageViewModel(
        IImageService imageService,
        IPopupService popupService,
        ILoadingService loadingService) : base(loadingService)
    {
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(NavigationParams.PromptSettings, out var promptSettings) ||
            promptSettings is not PromptSettings settings)
        {
            throw new ArgumentException(nameof(NavigationParams.PromptSettings));
        }

        _settings = settings.Clone();

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    [RelayCommand]
    private async Task ResetValues()
    {
        if (_settings == null)
        {
            return;
        }

        var result = await Shell.Current.DisplayAlertAsync("Confirm Reset", "Are you sure you would like to reset back to defaults?", "RESET", "Cancel");

        if (!result)
        {
            return;
        }

        cancelInitImageLoading();
        cancelMaskImageLoading();

        var defaultSettings = new PromptSettings();
        Strength = defaultSettings.DenoisingStrength.ToString();
        MaskBlur = defaultSettings.MaskBlur.ToString();
        _settings.InitImage = null;
        _settings.InitImageThumbnail = null;
        _settings.Mask = null;
        InitImageSource = null;
        MaskImageSource = null;
    }

    [RelayCommand]
    private async Task Cancel()
    {
        cancelInitImageLoading();
        cancelMaskImageLoading();

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task ConfirmSettings()
    {
        if (_settings == null)
        {
            return;
        }

        mapPropertiesToSettings();

        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings }, };

        await Shell.Current.GoToAsync("..", parameters);
    }

    public override bool OnBackButtonPressed()
    {
        ConfirmSettingsCommand.Execute(null);

        return true;
    }

    private async Task mapSettingsToProperties()
    {
        if (_settings == null)
        {
            return;
        }

        Strength = _settings.DenoisingStrength.ToString();
        MaskBlur = _settings.MaskBlur.ToString();

        FitImageServerSide = _settings.EnableFitServerSide;
        FitImageClientSide = _settings.FitClientSide;

        _initCancellationTokenSource = new CancellationTokenSource();
        _maskCancellationTokenSource = new CancellationTokenSource();

        await Task.WhenAll(new[] {
            Task.Run(async () =>
            {
                IsLoadingInitImage = true;
                var imageToLoad = !string.IsNullOrEmpty(_settings.InitImageThumbnail) ? _settings.InitImageThumbnail : _settings.InitImage;
                InitImageSource = await _imageService.GetImageSourceFromContentTypeStringAsync(imageToLoad, _initCancellationTokenSource.Token);
                IsLoadingInitImage = false;
            }, _initCancellationTokenSource.Token),
            Task.Run(async () =>
            {
                IsLoadingMaskImage = true;
                MaskImageSource = await _imageService.GetImageSourceFromContentTypeStringAsync(_settings.Mask, _maskCancellationTokenSource.Token);
                IsLoadingMaskImage = false;
            }, _maskCancellationTokenSource.Token),
        });
    }

    private void mapPropertiesToSettings()
    {
        if (_settings == null) return;

        if (double.TryParse(Strength, out var strength) ||
            double.TryParse(StrengthPlaceholder, out strength))
        {
            _settings.DenoisingStrength = strength;
        }

        if (int.TryParse(MaskBlur, out var maskBlur) ||
            int.TryParse(MaskBlurPlaceholder, out maskBlur))
        {
            _settings.MaskBlur = maskBlur;
        }

        _settings.EnableFitServerSide = FitImageServerSide;
        _settings.FitClientSide = FitImageClientSide;
    }

    [RelayCommand]
    private async Task ShowMediaPicker(bool forInitImage)
    {
        try
        {
            var fileResult = await MediaPicker.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 });
            var photo = fileResult?.FirstOrDefault();

            if (photo == null || _settings == null)
            {
                return;
            }

            if (forInitImage)
            {
                IsLoadingInitImage = true;
            }
            else
            {
                IsLoadingMaskImage = true;
            }

            using var fileStream = await photo.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();
            var imageString = Convert.ToBase64String(imageBytes);
            memoryStream.Seek(0, SeekOrigin.Begin);

            var formattedImageString = string.Format(Constants.ImageDataFormat, photo.ContentType, imageString);

            if (forInitImage)
            {
                cancelInitImageLoading();

                _settings.InitImage = formattedImageString;

                memoryStream.Seek(0, SeekOrigin.Begin);
                var bitmap = _imageService.GetSkBitmapFromStream(memoryStream);

                if (bitmap != null)
                {
                    _settings.InitImageThumbnail = _imageService.GetThumbnailString(bitmap, photo.ContentType);

                    // Attempt to match the aspect ratio of the image within the resolution constraints
                    var constrainedDimensions = MathHelper.GetAspectCorrectConstrainedDimensions(bitmap.Width, bitmap.Height, 0, 0, MathHelper.DimensionConstraint.ClosestMatch);

                    _settings.Width = constrainedDimensions.Width;
                    _settings.Height = constrainedDimensions.Height;

                    InitImageSource = new SKBitmapImageSource
                    {
                        Bitmap = bitmap
                    };
                }
            }
            else
            {
                cancelMaskImageLoading();

                _settings.Mask = formattedImageString;

                MaskImageSource = ImageSource.FromStream(() => memoryStream);
            }
        }
        catch (Exception)
        {
            await _popupService.DisplayAlertAsync("Error", "Failed to load image", "OK");
        }
        finally
        {
            IsLoadingInitImage = false;
            IsLoadingMaskImage = false;
        }
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();

        await mapSettingsToProperties();
    }

    private void cancelInitImageLoading()
    {
        if (_initCancellationTokenSource != null && !_initCancellationTokenSource.IsCancellationRequested)
        {
            _initCancellationTokenSource.Cancel();
        }
    }

    private void cancelMaskImageLoading()
    {
        if (_maskCancellationTokenSource != null && !_maskCancellationTokenSource.IsCancellationRequested)
        {
            _maskCancellationTokenSource.Cancel();
        }
    }
}
