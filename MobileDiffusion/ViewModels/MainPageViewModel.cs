using MobileDiffusion.Interfaces.ViewModels;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Models;
using System.Text.RegularExpressions;
using MobileDiffusion.Models.LStein;
using System.Collections.ObjectModel;
using MobileDiffusion.Views;
using CommunityToolkit.Maui.Views;

namespace MobileDiffusion.ViewModels;

public partial class MainPageViewModel : BaseViewModel, IMainPageViewModel
{
    private readonly IFileService _fileService;
    private readonly IStableDiffusionService _stableDiffusionService;

    [ObservableProperty]
    private string prompt;

    [ObservableProperty]
    private string placeholderPrompt = "A detailed painting of a kangaroo";

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

        var request = new BaseRequest
        {
            Prompt = finalPrompt,
            NumOutputs = 3,
            Width = 256,
            Height = 256
        };

        ImageLayoutWidth = MainLayoutWidth - MainLayoutPadding.HorizontalThickness;

        ImageWidth = request.NumOutputs switch
        {
            var x when x > 4 => ImageLayoutWidth,
            var x when x > 1 && x <= 4 => ImageLayoutWidth / 2.5,
            var x when x >= 0 => ImageLayoutWidth,
        };

        var ratio = request.Width / request.Height;

        ImageHeight = ImageWidth / ratio;

        var sanitizedPrompt = finalPrompt.Replace(" ", "_").ToLower();
        var length = Math.Min(sanitizedPrompt.Length, 100);
        var fileNameNoExtension = $"{sanitizedPrompt[..length]}-{request.Seed}";

        List<LSteinResponseItem> results = new();
        var imageSources = new List<ImageSource>();
        var imageNumber = 0;

        try
        {
            await foreach (var item in _stableDiffusionService.SubmitTextToImageRequest(request))
            {
                // TODO - Use "step" items to display progress
                if (!item.Event.Equals("result", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var imageBytes = await _stableDiffusionService.GetImageBytesAsync(item);

                var uri = await _fileService.WriteFileToInternalStorageAsync($"{fileNameNoExtension}-{imageNumber++}.png", imageBytes);

                ResultImageSources.Add(ImageSource.FromFile(uri));

                results.Add(item);
            }
        }
        catch (Exception e)
        {
            // TODO - Handle this

            return;
        }

        //ResultImageSources = imageSources;

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

        await Shell.Current.GoToAsync(nameof(PromptSettingsPage));

        await Task.CompletedTask;
    }
}
