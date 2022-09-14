using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using System.Threading;

namespace MobileDiffusion.ViewModels;

public partial class ImageToImageSettingsPageViewModel : PageViewModel, IImageToImageSettingsPageViewModel
{
    private Settings _settings;

    private CancellationTokenSource _initCancellationTokenSource;

    private CancellationTokenSource _maskCancellationTokenSource;

    [ObservableProperty]
    private bool isLoadingInitImage;

    [ObservableProperty]
    private bool isLoadingMaskImage;

    [ObservableProperty]
    private string strength;

    [ObservableProperty]
    private string strengthPlaceholder;

    [ObservableProperty]
    private ImageSource initImageSource;

    [ObservableProperty]
    private ImageSource maskImageSource;

    public ImageToImageSettingsPageViewModel()
    {
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(NavigationParams.PromptSettings, out var promptSettings) ||
            promptSettings is not Settings settings)
        {
            throw new ArgumentException(nameof(NavigationParams.PromptSettings));
        }

        _settings = settings.Clone();
    }

    [RelayCommand]
    private async Task ResetValues()
    {
        var result = await Shell.Current.DisplayAlert("Confirm Reset", "Are you sure you would like to reset back to defaults?", "RESET", "Cancel");

        if (!result)
        {
            return;
        }

        cancelInitImageLoading();
        cancelMaskImageLoading();

        var defaultSettings = new Settings();
        Strength = defaultSettings.PromptStrength.ToString();
        _settings.InitImage = null;
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
        mapPropertiesToSettings();

        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        await Shell.Current.GoToAsync("..", parameters);
    }

    private async void mapSettingsToProperties()
    {
        Strength = _settings.PromptStrength.ToString();

        _initCancellationTokenSource = new CancellationTokenSource();
        _maskCancellationTokenSource = new CancellationTokenSource();

        await Task.WhenAll(new[] {
            Task.Run(async () =>
            {
                IsLoadingInitImage = true;
                InitImageSource = await getImageSourceFromString(_settings.InitImage, _initCancellationTokenSource.Token);
                IsLoadingInitImage = false;
            }, _initCancellationTokenSource.Token),
            Task.Run(async () =>
            {
                IsLoadingMaskImage = true;
                MaskImageSource = await getImageSourceFromString(_settings.Mask, _maskCancellationTokenSource.Token);
                IsLoadingMaskImage = false;
            }, _maskCancellationTokenSource.Token),
        });
    }

    private Task<ImageSource> getImageSourceFromString(string imageString, CancellationToken token)
    {
        if (string.IsNullOrEmpty(imageString))
        {
            return Task.FromResult<ImageSource>(null);
        }

        var matchResult = Constants.ImageDataRegex.Match(imageString);

        if (matchResult.Success)
        {
            try
            {
                var imageBase64 = matchResult.Groups[Constants.ImageDataCaptureGroupData].Value;

                var imageBytes = Convert.FromBase64String(imageBase64);

                if (token.IsCancellationRequested)
                {
                    return Task.FromResult<ImageSource>(null);
                }

                var memoryStream = new MemoryStream(imageBytes);

                return Task.FromResult(ImageSource.FromStream(() => memoryStream));
            }
            catch
            {
                // TODO - Handle exceptions
            }
        }

        return Task.FromResult<ImageSource>(null);
    }

    private void mapPropertiesToSettings()
    {
        if (double.TryParse(Strength, out var strength) ||
            double.TryParse(StrengthPlaceholder, out strength))
        {
            _settings.PromptStrength = strength;
        }
    }

    [RelayCommand]
    private async Task ShowMediaPicker(bool forInitImage)
    {
        try
        {
            var fileResult = await MediaPicker.PickPhotoAsync();

            if (fileResult == null)
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
        
            using var fileStream = await fileResult.OpenReadAsync();
            var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();
            var imageString = Convert.ToBase64String(imageBytes);
            memoryStream.Seek(0, SeekOrigin.Begin);

            var formattedImageString = string.Format(Constants.ImageDataFormat, fileResult.ContentType, imageString);

            if (forInitImage)
            {
                cancelInitImageLoading();

                _settings.InitImage = formattedImageString;

                InitImageSource = ImageSource.FromStream(() => memoryStream);
            }
            else
            {
                cancelMaskImageLoading();

                _settings.Mask = formattedImageString;

                MaskImageSource = ImageSource.FromStream(() => memoryStream);
            }
        }
        catch
        {
            // TODO - Handle exceptions
        }
        finally
        {
            IsLoadingInitImage = false;
            IsLoadingMaskImage = false;
        }
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();

        mapSettingsToProperties();
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
