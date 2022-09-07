using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.ViewModels;

public partial class ImageToImageSettingsPopupViewModel : PopupBaseViewModel, IImageToImageSettingsPopupViewModel
{
    private Settings _settings;

    [ObservableProperty]
    private string strength;

    [ObservableProperty]
    private string strengthPlaceholder;

    [ObservableProperty]
    private ImageSource initImageSource;

    public ImageToImageSettingsPopupViewModel(IPopupService popupService) : base(popupService)
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

        mapSettingsToProperties();
    }

    [RelayCommand]
    private void ResetValues()
    {
        var defaultSettings = new Settings();
        Strength = defaultSettings.PromptStrength.ToString();
        _settings.InitImage = null;
        InitImageSource = null;
    }

    [RelayCommand]
    private void Cancel()
    {
        ClosePopup(null);
    }

    [RelayCommand]
    private void ConfirmSettings()
    {
        mapPropertiesToSettings();

        ClosePopup(_settings);
    }

    private void mapSettingsToProperties()
    {
        Strength = _settings.PromptStrength.ToString();

        if (!string.IsNullOrEmpty(_settings.InitImage))
        {
            var matchResult = Constants.ImageDataRegex.Match(_settings.InitImage);

            if (matchResult.Success)
            {
                try
                {
                    var imageBase64 = matchResult.Groups[Constants.ImageDataCaptureGroupData].Value;

                    var imageBytes = Convert.FromBase64String(imageBase64);

                    var memoryStream = new MemoryStream(imageBytes);

                    InitImageSource = ImageSource.FromStream(() => memoryStream);
                }
                catch
                {
                    // TODO - Handle exceptions
                }
            }
        }
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
    private async Task ShowMediaPicker()
    {
        try
        {
            var fileResult = await MediaPicker.PickPhotoAsync();

            if (fileResult == null)
            {
                return;
            }
        
            using var fileStream = await fileResult.OpenReadAsync();
            var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();
            var imageString = Convert.ToBase64String(imageBytes);

            _settings.InitImage = string.Format(Constants.ImageDataFormat, fileResult.ContentType, imageString);

            memoryStream.Seek(0, SeekOrigin.Begin);
            InitImageSource = ImageSource.FromStream(() => memoryStream);
        }
        catch
        {
            // TODO - Handle exceptions
        }
    }
}
