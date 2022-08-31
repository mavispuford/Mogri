using MobileDiffusion.Interfaces.ViewModels;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Models;
using System.Text.RegularExpressions;

namespace MobileDiffusion.ViewModels;

public partial class MainPageViewModel : BaseViewModel, IMainPageViewModel
{
    private Regex imageDataRegex = new Regex("^data:((?<type>[\\w\\/]+))?;base64,(?<data>.+)$", RegexOptions.Compiled);

    private readonly IFileService _fileService;
    private readonly IStableDiffusionService _stableDiffusionService;

    [ObservableProperty]
    private string prompt;

    [ObservableProperty]
    private string placeholderPrompt = "A detailed painting of a kangaroo";

    [ObservableProperty]
    private List<ImageSource> resultImageSources = new();

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
        ResultImageSources = null;

        var finalPrompt = string.IsNullOrEmpty(Prompt) ? PlaceholderPrompt : Prompt;

        var request = new TextToImageRequest
        {
            Prompt = finalPrompt,
            NumOutputs = 1,
            Width = "256",
            Height = "256"
        };

        if (request.InitImage == null)
        {
            request.PromptStrength = null;
        }

        List<string> results;

        try
        {
            results = (await _stableDiffusionService.SubmitTextToImageRequest(request)).ToList();
        }
        catch (Exception e)
        {
            // TODO - Handle this

            return;
        }

        var sanitizedPrompt = finalPrompt.Replace(" ", "_").ToLower();
        var length = Math.Min(sanitizedPrompt.Length, 100);
        var fileNameNoExtension = $"{sanitizedPrompt[..length]}-{request.Seed}";

        var uris = new List<string>();

        try
        {
            foreach (var result in results)
            {
                var imageDataMatch = imageDataRegex.Match(result);

                if (!imageDataMatch.Success)
                {
                    continue;
                }

                var imageData = imageDataMatch.Groups["data"].Value;

                var dataBytes = Convert.FromBase64String(imageData);

                using (var stream = new MemoryStream())
                {
                    await stream.WriteAsync(dataBytes);

                    var uri = await _fileService.WriteFileToInternalStorageAsync($"{fileNameNoExtension}-{results.IndexOf(result)}.png", stream);

                    uris.Add(uri);
                }           
            }
        }
        catch (Exception e)
        {
            // TODO - Handle this

            return;
        }

        var imageSources = new List<ImageSource>();

        try
        {
            foreach (var uri in uris)
            {
                //if (uris.Count == 1)
                //{
                //    // Testing 4 images
                //    for (var i = 0; i < 3; i++)
                //    {
                //        imageSources.Add(ImageSource.FromFile(uri));
                //    }
                //}

                imageSources.Add(ImageSource.FromFile(uri));
            }
        }
        catch (Exception e)
        {
            // TODO - Handle this

            return;
        }

        ImageWidth = imageSources.Count switch
        {
            var x when x > 4 => ImageLayoutWidth,
            var x when x > 1 && x <= 4 => ImageLayoutWidth / 2.5,
            var x when x >= 0 => ImageLayoutWidth,
        };

        double.TryParse(request.Width, out var requestWidth);
        double.TryParse(request.Height, out var requestHeight);

        var ratio = requestWidth / requestHeight;

        ImageHeight = ImageWidth / ratio;

        ResultImageSources = imageSources;

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
        await Task.CompletedTask;
    }
}
