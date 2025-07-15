using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class ColorPickerPopupViewModel : PopupBaseViewModel, IColorPickerPopupViewModel
{
    [ObservableProperty]
    private Color _currentColor;

    [ObservableProperty]
    private string _currentColorHexString;

    [ObservableProperty]
    private List<Color> _swatches = new();

    [ObservableProperty]
    private List<Color> _swatchesFromImage = new();

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

    partial void OnCurrentColorHexStringChanged(string value)
    {
        try
        {
            var hexColor = Color.FromArgb(value);

            if (CurrentColor != hexColor)
            {
                CurrentColor = hexColor;
            }
        }
        catch
        {
            // Invalid color
        }
    }

    [RelayCommand]
    private async Task Cancel()
    {
        await ClosePopupAsync();
    }

    [RelayCommand]
    private async Task Confirm()
    {
        await ClosePopupAsync(CurrentColor);
    }

    [RelayCommand]
    private void SwatchTapped(Color color)
    {
        color ??= Colors.Black;

        CurrentColor = color;
        CurrentColorHexString = CurrentColor.ToHex();
    }
}
