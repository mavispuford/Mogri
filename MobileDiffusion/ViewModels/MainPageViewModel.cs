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
    private float progress;

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

        var settings = _settings.Clone();

        for (var i = 0; i < settings.NumOutputs; i++)
        {
            var resultItem = _serviceProvider.GetService<IResultItemViewModel>();

            resultItem.SetSettingsCommand = new RelayCommand<Settings>(setSettingsFromResultItem);
            resultItem.SetInitImageCommand = new RelayCommand<string>(setInitImageFromResultItem);

            Results.Add(resultItem);
        }

        var finalPrompt = string.IsNullOrEmpty(Prompt) ? PlaceholderPrompt : Prompt;

        settings.Prompt = finalPrompt;

        var sanitizedPrompt = finalPrompt.Replace(" ", "_").ToLower();
        var length = Math.Min(sanitizedPrompt.Length, 90);

        var imageNumber = 0;

        try
        {
            Progress = 0;

            await foreach (var item in _stableDiffusionService.SubmitTextToImageRequest(settings))
            {
                if (item.Event.Equals("step", StringComparison.OrdinalIgnoreCase))
                {
                    Progress = item.Step / (float)(settings.NumInferenceSteps);

                    continue;
                }

                var fileNameNoExtension = $"{sanitizedPrompt[..length]}-{item.Seed}-{DateTime.Now.Millisecond}";

                if (item.Event.Equals("result", StringComparison.OrdinalIgnoreCase))
                {
                    var result = Results.FirstOrDefault(r => r.ResponseItem == null);

                    result.ResponseItem = item;

                    await retrieveResultImageAsync(result, fileNameNoExtension, imageNumber++);
                }
                else if (item.Event.Equals("upscaling-started", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var result in Results)
                    {
                        result.IsLoading = true;
                    }
                }
                else if (item.Event.Equals("upscaling-done", StringComparison.OrdinalIgnoreCase))
                {
                    foreach(var result in Results)
                    {
                        if (result != null)
                        {
                            await retrieveResultImageAsync(result, fileNameNoExtension, imageNumber++);
                        }
                    }                    
                }
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

        // Any remaining results that weren't set have failed
        foreach (var result in Results)
        {
            if (result.IsLoading)
            {
                result.IsLoading = false;
                result.Failed = true;
            }
        }
    }

    private async Task retrieveResultImageAsync(IResultItemViewModel result, string fileName, int number)
    {
        var imageBytes = await _stableDiffusionService.GetImageBytesAsync(result.ResponseItem);

        var uri = await _fileService.WriteFileToInternalStorageAsync($"{fileName}-{number++}.png", imageBytes);

        result.InternalUri = uri;
        result.ImageSource = ImageSource.FromFile(uri);
        result.IsLoading = false;
    }

    [RelayCommand]
    private async Task ShowImageToImageSettings()
    {
        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        await Shell.Current.GoToAsync("ImageToImageSettingsPage", parameters);
    }

    [RelayCommand]
    private async Task ShowRequestSettings()
    {
        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        await Shell.Current.GoToAsync("PromptSettingsPage", parameters);
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
        // Possible workaround for the following bugs:
        // https://github.com/dotnet/maui/issues/9011
        // https://github.com/dotnet/maui/issues/8809
        // However, it doesn't work because of this bug:
        // https://github.com/dotnet/maui/issues/8787

        //refreshImageSources();
    }

    public override void OnDisappearing()
    {
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
