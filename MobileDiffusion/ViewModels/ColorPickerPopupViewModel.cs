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

    [ObservableProperty]
    private List<Color> swatchesFromImage = new();

    public ColorPickerPopupViewModel(IPopupService popupService) : base(popupService)
    {
        Swatches = new()
        {
            Colors.Black,
            Colors.Grey,
            Colors.White,
            Colors.Red,
            Colors.OrangeRed,
            Colors.Pink,
            Colors.Yellow,
            Colors.Green,
            Colors.DarkGreen,
            Colors.Blue,
            Colors.SkyBlue,
            Colors.DarkBlue,
            Colors.Purple,
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
            SwatchesFromImage = colorPalette;
        }

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
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
