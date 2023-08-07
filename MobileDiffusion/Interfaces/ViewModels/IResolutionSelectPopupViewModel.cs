using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IResolutionSelectPopupViewModel : IPopupBaseViewModel
{
    double AspectRatioDouble { get; set; }

    string AspectRatioString { get; set; }

    string AspectRatioEntryValue { get; set; }

    double Width { get; set; }

    string WidthEntryValue { get; set; }

    double WidthSliderValue { get; set; }

    double Height { get; set; }

    string HeightEntryValue { get; set; }

    double HeightSliderValue { get; set; }

    bool PreserveAspectRatio { get; set; }

    ImageSource InitImageSource { get; set; }

    double ExampleRectangleContainerWidth { get; set; }

    double ExampleRectangleContainerHeight { get; set; }

    double ExampleRectangleWidth { get; set; }

    double ExampleRectangleHeight { get; set; }

    double MinimumWidthHeight { get; set; }

    double MaximumWidthHeight { get; set; }

    IAsyncRelayCommand CancelCommand { get; }

    IAsyncRelayCommand ConfirmCommand { get; }

    IRelayCommand<string[]> UpdateAllValuesCommand { get; }
}
