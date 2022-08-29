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
        //await _stableDiffusionService.SubmitTextToImageRequest(new TextToImageRequest
        //{
        //    Prompt = Prompt
        //});

        var fakeResults = new List<ImageSource>();
        for(var i = 0; i < 6; i++)
        {
            fakeResults.Add(ImageSource.FromFile("dotnet_bot"));
        }

        ResultImageSources = fakeResults;
    }
}
