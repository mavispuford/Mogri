using MobileDiffusion.Interfaces.ViewModels;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Models;
using MobileDiffusion.Models.LStein;
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
    private string prompt;

    [ObservableProperty]
    private string placeholderPrompt = "An astronaut floating in space, detailed digital drawing, octane render, trending on artstation";

    [ObservableProperty]
    private ObservableCollection<ImageSource> resultImageSources = new();

    [ObservableProperty]
    private ObservableCollection<IResultItemViewModel> results = new();

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
        ResultImageSources = new();
        Results = new();

        for(var i = 0; i < _settings.NumOutputs; i++)
        {
            Results.Add(_serviceProvider.GetService<IResultItemViewModel>());
        }

        var finalPrompt = string.IsNullOrEmpty(Prompt) ? PlaceholderPrompt : Prompt;

        _settings.Prompt = finalPrompt;

        ImageLayoutWidth = MainLayoutWidth - MainLayoutPadding.HorizontalThickness;

        bool exceptionThrown;
        do
        {
            try
            {
                exceptionThrown = false;

                ImageWidth = _settings.NumOutputs switch
                {
                    var x when x > 4 => ImageLayoutWidth,
                    var x when x > 1 && x <= 4 => ImageLayoutWidth / 2.5,
                    var x when x >= 0 => ImageLayoutWidth,
                };

                var ratio = _settings.Width / _settings.Height;

                ImageHeight = ImageWidth / ratio;
            }
            catch
            {
                exceptionThrown = true;
                // Null reference exceptions can be thrown here on the view side because
                // image items in the itemtemplate can be disposed but somehow still linked
            }
        } while (exceptionThrown);

        var sanitizedPrompt = finalPrompt.Replace(" ", "_").ToLower();
        var length = Math.Min(sanitizedPrompt.Length, 100);

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

                var resultWithNullImageSource = Results.FirstOrDefault(r => r.ImageSource == null);

                resultWithNullImageSource.ImageSource = ImageSource.FromFile(uri);
                resultWithNullImageSource.Config = item;
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
        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        var settings = await _popupService.ShowPopupAsync("PromptSettingsPopup", parameters) as Settings;

        if (settings != null)
        {
            _settings = settings;
        }
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(NavigationParams.PromptSettings, out var promptSettings) &&
            promptSettings is Settings settings)
        {
            _settings = settings;
        }
    }
}
