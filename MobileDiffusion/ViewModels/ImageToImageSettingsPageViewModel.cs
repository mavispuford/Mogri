using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.ViewModels;

public partial class ImageToImageSettingsPageViewModel : PageViewModel, IImageToImageSettingsPageViewModel
{
    private Settings _settings;

    [ObservableProperty]
    private string strength;

    [ObservableProperty]
    private string strengthPlaceholder;

    [ObservableProperty]
    private ImageSource initImageSource;

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

        mapSettingsToProperties();
    }

    [RelayCommand]
    private async Task ResetValues()
    {
        var result = await Shell.Current.DisplayAlert("Confirm Reset", "Are you sure you would like to reset back to defaults?", "RESET", "Cancel");

        if (!result)
        {
            return;
        }

        var defaultSettings = new Settings();
        Strength = defaultSettings.PromptStrength.ToString();
        _settings.InitImage = null;
        InitImageSource = null;
    }

    [RelayCommand]
    private async Task Cancel()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task ConfirmSettings()
    {
        mapPropertiesToSettings();

        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        await Shell.Current.GoToAsync("..", parameters);
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
