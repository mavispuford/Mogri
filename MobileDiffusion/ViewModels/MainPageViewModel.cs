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
    private ObservableCollection<IDrawingLine> maskLines;

    [ObservableProperty]
    private double imageWidth;

    [ObservableProperty]
    private double imageHeight;

    public MainPageViewModel(IFileService fileService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
    }

    [RelayCommand]
    private async Task SaveMaskAsync()
    {
        if (MaskLines == null)
        {
            return;
        }

        using var stream = await DrawingView.GetImageStream(MaskLines, new Size(ImageWidth, ImageHeight), Colors.Black.AsPaint());

        await _fileService.WriteFileToExternalStorageAsync("mask.png", stream);
    }
}
