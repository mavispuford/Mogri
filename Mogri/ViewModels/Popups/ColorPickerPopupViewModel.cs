using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Popups;

namespace Mogri.ViewModels;

public partial class ColorPickerPopupViewModel : PopupBaseViewModel, IColorPickerPopupViewModel
{
    [ObservableProperty]
    public partial Color CurrentColor { get; set; }

    [ObservableProperty]
    public partial string CurrentColorHexString { get; set; }

    [ObservableProperty]
    public partial List<Color> Swatches { get; set; } = new();

    [ObservableProperty]
    public partial List<Color> SwatchesFromImage { get; set; } = new();

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
