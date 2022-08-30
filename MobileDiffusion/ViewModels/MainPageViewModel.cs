using MobileDiffusion.Interfaces.ViewModels;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Models;

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
    private List<ImageSource> resultImageSources = new();

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
        var finalPrompt = string.IsNullOrEmpty(Prompt) ? PlaceholderPrompt : Prompt;

        var request = new TextToImageRequest
        {
            Prompt = finalPrompt
        };

        var results = new List<string>();

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
                using MemoryStream stream = new MemoryStream();

                using StreamWriter writer = new StreamWriter(stream);

                await writer.WriteAsync(result);

                var uri = await _fileService.WriteFileToInternalStorageAsync($"{fileNameNoExtension}-{results.IndexOf(result)}.png", stream);

                uris.Add(uri);
            }
        }
        catch(Exception e)
        {
            // TODO - Handle this

            return;
        }

        var imageSources = new List<ImageSource>();

        try
        {
            foreach (var uri in uris)
            {
                var stream = await _fileService.GetFileStreamFromInternalStorage(Path.GetFileName(uri));

                imageSources.Add(ImageSource.FromStream(() => stream));
            }
        }
        catch (Exception e)
        {
            // TODO - Handle this

            return;
        }

        ResultImageSources = imageSources;

        //var fakeResults = new List<ImageSource>();
        //for(var i = 0; i < 6; i++)
        //{
        //    fakeResults.Add(ImageSource.FromFile("dotnet_bot"));
        //}

        //ResultImageSources = fakeResults;
    }
}
