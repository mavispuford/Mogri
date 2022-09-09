using MobileDiffusion.Interfaces.ViewModels;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Models;
using System.Collections.ObjectModel;

namespace MobileDiffusion.ViewModels;

public partial class MainPageViewModel : PageViewModel, IMainPageViewModel, IQueryAttributable
{
    private readonly IFileService _fileService;
    private readonly IStableDiffusionService _stableDiffusionService;
    private readonly IPopupService _popupService;
    private readonly IServiceProvider _serviceProvider;

    private Settings _settings = new();

    [ObservableProperty]
    private bool hasInitImage;

    [ObservableProperty]
    private string prompt;

    [ObservableProperty]
    private string placeholderPrompt = "An astronaut floating in space, detailed digital drawing, octane render, trending on artstation";

    [ObservableProperty]
    private ObservableCollection<IResultItemViewModel> results = new();

    public MainPageViewModel(
        IFileService fileService,
        IStableDiffusionService stableDiffusionService,
        IPopupService popupService,
        IServiceProvider serviceProvider)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    [RelayCommand]
    private async Task Create()
    {
        Results = new();

        for(var i = 0; i < _settings.NumOutputs; i++)
        {
            var resultItem = _serviceProvider.GetService<IResultItemViewModel>();

            resultItem.SetSettingsCommand = new RelayCommand<Settings>(setSettingsFromResultItem);
            resultItem.SetInitImageCommand = new RelayCommand<string>(setInitImageFromResultItem);

            Results.Add(resultItem);
        }

        var finalPrompt = string.IsNullOrEmpty(Prompt) ? PlaceholderPrompt : Prompt;

        _settings.Prompt = finalPrompt;

        var sanitizedPrompt = finalPrompt.Replace(" ", "_").ToLower();
        var length = Math.Min(sanitizedPrompt.Length, 90);

        var imageNumber = 0;

        try
        {
            await foreach (var item in _stableDiffusionService.SubmitTextToImageRequest(_settings))
            {
                // TODO - Use "step" items to display progress
                if (!item.Event.Equals("result", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var imageBytes = await _stableDiffusionService.GetImageBytesAsync(item);

                var fileNameNoExtension = $"{sanitizedPrompt[..length]}-{item.Seed}-{DateTime.Now.Millisecond}";

                var uri = await _fileService.WriteFileToInternalStorageAsync($"{fileNameNoExtension}-{imageNumber++}.png", imageBytes);

                var resultWithNullUri = Results.FirstOrDefault(r => string.IsNullOrEmpty(r.InternalUri));

                resultWithNullUri.InternalUri = uri;
                resultWithNullUri.ImageSource = ImageSource.FromFile(uri);
                resultWithNullUri.ResponseItem = item;
            }
        }
        catch (System.Net.WebException webException)
        {
            // TODO - Handle this

            return;
        }
        catch (Exception e)
        {
            // TODO - Handle this

            return;
        }
    }

    [RelayCommand]
    private async Task ShowImageToImageSettings()
    {
        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        var settings = await _popupService.ShowPopupAsync("ImageToImageSettingsPopup", parameters) as Settings;

        if (settings != null)
        {
            _settings = settings;

            updateHasInitImage();
        }
    }

    [RelayCommand]
    private async Task ShowRequestSettings()
    {
        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        var settings = await _popupService.ShowPopupAsync("PromptSettingsPopup", parameters) as Settings;

        if (settings != null)
        {
            _settings = settings;

            updateHasInitImage();
        }
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(NavigationParams.PromptSettings, out var promptSettings) &&
            promptSettings is Settings settings)
        {
            _settings = settings;

            updateHasInitImage();
        }
    }

    private void setSettingsFromResultItem(Settings settings)
    {
        if (settings == null)
        {
            return;
        }

        var initImage = string.Empty;

        if (string.IsNullOrEmpty(settings.InitImage) &&
            !string.IsNullOrEmpty(_settings.InitImage))
        {
            initImage = _settings.InitImage;
        }

        _settings = settings;

        if (!string.IsNullOrEmpty(initImage))
        {
            _settings.InitImage = initImage;
        }

        updateHasInitImage();
    }

    private void setInitImageFromResultItem(string initImage)
    {
        if (string.IsNullOrEmpty(initImage))
        {
            return;
        }

        _settings.InitImage = initImage;

        updateHasInitImage();
    }

    private void updateHasInitImage()
    {
        HasInitImage = !string.IsNullOrEmpty(_settings?.InitImage);
    }


    public override void OnAppearing()
    {
        base.OnResume();

        // Possible workaround for the following bugs:
        // https://github.com/dotnet/maui/issues/9011
        // https://github.com/dotnet/maui/issues/8809
        // However, it doesn't work because of this bug:
        // https://github.com/dotnet/maui/issues/8787

        //refreshImageSources();
    }

    public override void OnDisappearing()
    {
        base.OnSleep();

        // Possible workaround for the following bugs:
        // https://github.com/dotnet/maui/issues/9011
        // https://github.com/dotnet/maui/issues/8809
        // However, it doesn't work because of this bug:
        // https://github.com/dotnet/maui/issues/8787

        //clearImageSources();
    }

    private void clearImageSources()
    {
        foreach (var result in Results)
        {
            result.ImageSource = default(ImageSource);
        }
    }

    private void refreshImageSources()
    {
        foreach (var result in Results)
        {
            if (string.IsNullOrEmpty(result.InternalUri))
            {
                continue;
            }

            result.ImageSource = ImageSource.FromFile(result.InternalUri);
        }
    }
}
