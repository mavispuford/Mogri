using MobileDiffusion.Interfaces.ViewModels;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Models;
using MobileDiffusion.Models.LStein;
using System.Collections.ObjectModel;
using MobileDiffusion.Views;

namespace MobileDiffusion.ViewModels;

public partial class MainPageViewModel : BaseViewModel, IMainPageViewModel, IQueryAttributable
{
    private readonly IFileService _fileService;
    private readonly IStableDiffusionService _stableDiffusionService;

    private Settings _settings = new();

    [ObservableProperty]
    private string prompt;

    [ObservableProperty]
    private string placeholderPrompt = "An astronaut floating in space, detailed digital drawing, octane render, trending on artstation";

    [ObservableProperty]
    private ObservableCollection<ImageSource> resultImageSources = new();

    [ObservableProperty]
    private double mainLayoutWidth;

    [ObservableProperty]
    private Thickness mainLayoutPadding;

    [ObservableProperty]
    private double imageLayoutWidth;

    [ObservableProperty]
    private double imageWidth;
    
    [ObservableProperty]
    private double imageHeight;

    public MainPageViewModel(
        IFileService fileService,
        IStableDiffusionService stableDiffusionService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
    }

    [RelayCommand]
    private async Task Create()
    {
        ResultImageSources = new();

        var finalPrompt = string.IsNullOrEmpty(Prompt) ? PlaceholderPrompt : Prompt;

        _settings.Prompt = finalPrompt;

        ImageLayoutWidth = MainLayoutWidth - MainLayoutPadding.HorizontalThickness;

        ImageWidth = _settings.NumOutputs switch
        {
            var x when x > 4 => ImageLayoutWidth,
            var x when x > 1 && x <= 4 => ImageLayoutWidth / 2.5,
            var x when x >= 0 => ImageLayoutWidth,
        };

        var ratio = _settings.Width / _settings.Height;

        ImageHeight = ImageWidth / ratio;

        var sanitizedPrompt = finalPrompt.Replace(" ", "_").ToLower();
        var length = Math.Min(sanitizedPrompt.Length, 100);

        List<LSteinResponseItem> results = new();
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

                var fileNameNoExtension = $"{sanitizedPrompt[..length]}-{item.Seed}";

                var uri = await _fileService.WriteFileToInternalStorageAsync($"{fileNameNoExtension}-{imageNumber++}.png", imageBytes);

                ResultImageSources.Add(ImageSource.FromFile(uri));

                results.Add(item);
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

        //var fakeResults = new List<ImageSource>();
        //for (var i = 0; i < 4; i++)
        //{
        //    fakeResults.Add(ImageSource.FromFile("dotnet_bot"));
        //}

        //ResultImageSources = fakeResults;
    }

    [RelayCommand]
    private async Task ShowRequestSettings()
    {
        //var popup = new PromptSettingsPopup();
        //var result = await Shell.Current.CurrentPage.ShowPopupAsync(popup);

        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        await Shell.Current.GoToAsync(nameof(PromptSettingsPage), parameters);

        await Task.CompletedTask;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(NavigationParams.PromptSettings, out var promptSettings) &&
            promptSettings is Settings settings)
        {
            _settings = settings;
        }
    }
}
