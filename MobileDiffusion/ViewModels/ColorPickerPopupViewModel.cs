using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using System.ComponentModel;

namespace MobileDiffusion.ViewModels;

public partial class ColorPickerPopupViewModel : PopupBaseViewModel, IColorPickerPopupViewModel
{
    [ObservableProperty]
    private Color currentColor;

    [ObservableProperty]
    private string currentColorHexString;

    [ObservableProperty]
    private List<Color> swatches = new();

    public ColorPickerPopupViewModel(IPopupService popupService) : base(popupService)
    {
        Swatches = new()
        {
            Colors.Black,
            Colors.White,
            Colors.DarkRed,
            Colors.Red,
            Colors.DarkGreen,
            Colors.Green,
            Colors.DarkBlue,
            Colors.Blue,
        };
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);

        if (query.TryGetValue(NavigationParams.Color, out var colorParam) &&
            colorParam is Color color)
        {
            CurrentColor = color;
            CurrentColorHexString = CurrentColor.ToHex();
        }

        if (query.TryGetValue(NavigationParams.ColorPalette, out var colorPaletteParam) &&
            colorPaletteParam is List<Color> colorPalette)
        {
            Swatches = colorPalette;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(CurrentColorHexString))
        {
            var hexColor = Color.FromArgb(CurrentColorHexString);

            if (CurrentColor != hexColor)
            {
                CurrentColor = hexColor;
            }
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        ClosePopup();
    }

    [RelayCommand]
    private void Confirm()
    {
        ClosePopup(CurrentColor);
    }

    [RelayCommand]
    private void SwatchTapped(Color color)
    {
        CurrentColor = color;
        CurrentColorHexString = CurrentColor.ToHex();
    }
}
