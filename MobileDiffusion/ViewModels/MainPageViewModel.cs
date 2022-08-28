using MobileDiffusion.Interfaces.ViewModels;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;

namespace MobileDiffusion.ViewModels;

public partial class MainPageViewModel : BaseViewModel, IMainPageViewModel
{
    private readonly IFileService _fileService;

    [ObservableProperty]
    private string prompt;

    [ObservableProperty]
    private List<ImageSource> resultImageSources = new();

    public MainPageViewModel(IFileService fileService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
    }

    [RelayCommand]
    private async Task Create()
    {
        var fakeResults = new List<ImageSource>();
        for(var i = 0; i < 6; i++)
        {
            fakeResults.Add(ImageSource.FromFile("dotnet_bot"));
        }

        ResultImageSources = fakeResults;
    }
}
