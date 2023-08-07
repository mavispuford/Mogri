using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class ResolutionSelectPopupViewModel : PopupBaseViewModel, IResolutionSelectPopupViewModel
{
    [ObservableProperty]
    private double _aspectRatioDouble;

    [ObservableProperty]
    private string _aspectRatioString;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;

    [ObservableProperty]
    private double _widthSliderValue;

    [ObservableProperty]
    private double _heightSliderValue;

    [ObservableProperty]
    private string _widthEntryValue;

    [ObservableProperty]
    private string _heightEntryValue;

    [ObservableProperty]
    private bool _preserveAspectRatio = true;

    [ObservableProperty]
    private ImageSource _initImageSource;

    [ObservableProperty]
    private double _exampleRectangleContainerWidth;

    [ObservableProperty]
    private double _exampleRectangleContainerHeight;

    [ObservableProperty]
    private double _exampleRectangleWidth;

    [ObservableProperty]
    private double _exampleRectangleHeight;

    [ObservableProperty]
    private double _minimumWidthHeight = 64;

    [ObservableProperty]
    private double _maximumWidthHeight = 2048;

    public ResolutionSelectPopupViewModel(IPopupService popupService) : base(popupService)
    {
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);

        if (query.TryGetValue(NavigationParams.Width, out var widthParam) &&
            widthParam is double width)
        {
            Width = width;
        }
        else if (widthParam is string widthString &&
            double.TryParse(widthString, out double widthParsed))
        {
            Width = widthParsed;
        }
        else
        {
            throw new ArgumentNullException(nameof(width));
        }

        if (query.TryGetValue(NavigationParams.Height, out var heightParam) &&
            heightParam is double height)
        {
            Height = height;
        }
        else if (heightParam is string heightString &&
            double.TryParse(heightString, out double heightParsed))
        {
            Height = heightParsed;
        }
        else
        {
            throw new ArgumentNullException(nameof(height));
        }

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();

        calculateAspectRatio();
        UpdateAllValues();
    }

    [RelayCommand]
    private void Cancel()
    {
        ClosePopup();
    }

    [RelayCommand]
    private void Confirm()
    {
        var parameters = new Dictionary<string, object>
        {
            { NavigationParams.Width, Width },
            { NavigationParams.Height, Height }
        };

        ClosePopup(parameters);
    }

    partial void OnWidthSliderValueChanged(double value)
    {
        updateWidth(value);
    }

    partial void OnHeightSliderValueChanged(double value)
    {
        updateHeight(value);
    }

    partial void OnWidthEntryValueChanged(string value)
    {
        if (double.TryParse(value, out double width))
        {
            updateWidth(width, excludeFromUpdate: nameof(WidthEntryValue));
        }
    }

    partial void OnHeightEntryValueChanged(string value)
    {
        if (double.TryParse(value, out double height))
        {
            updateHeight(height, excludeFromUpdate: nameof(HeightEntryValue));
        }
    }

    private void updateWidth(double value, bool aspectCorrectedValue = false, params string[] excludeFromUpdate)
    {
        var roundedValue = double.Clamp(Math.Round(value / 64) * 64, MinimumWidthHeight, MaximumWidthHeight);
        Width = roundedValue;

        if (!aspectCorrectedValue)
        {
            if (PreserveAspectRatio)
            {
                var calculatedHeight = Width / AspectRatioDouble;

                updateHeight(calculatedHeight, true);
            }
            else
            {
                calculateAspectRatio();
            }

            UpdateAllValues(excludeFromUpdate);
        }

        updateExampleRectangle();
    }

    private void updateHeight(double value, bool aspectCorrectedValue = false, params string[] excludeFromUpdate)
    {
        var roundedValue = double.Clamp(Math.Round(value / 64) * 64, MinimumWidthHeight, MaximumWidthHeight);
        Height = roundedValue;

        if (!aspectCorrectedValue)
        {
            if (PreserveAspectRatio)
            {
                var calculatedWidth = Height * AspectRatioDouble;

                updateWidth(calculatedWidth, true);
            }
            else
            {
                calculateAspectRatio();
            }

            UpdateAllValues(excludeFromUpdate);
        }

        updateExampleRectangle();
    }

    [RelayCommand]
    private void UpdateAllValues(params string[] excludeFromUpdate)
    {
#pragma warning disable MVVMTK0034 // Direct field reference to [ObservableProperty] backing field

        if (excludeFromUpdate == null || !excludeFromUpdate.Contains(nameof(WidthSliderValue)))
        {
            _widthSliderValue = Width;
            OnPropertyChanged(nameof(WidthSliderValue));
        }

        if (excludeFromUpdate == null || !excludeFromUpdate.Contains(nameof(WidthEntryValue)))
        {
            _widthEntryValue = Width.ToString();
            OnPropertyChanged(nameof(WidthEntryValue));
        }

        if (excludeFromUpdate == null || !excludeFromUpdate.Contains(nameof(HeightSliderValue)))
        {
            _heightSliderValue = Height;
            OnPropertyChanged(nameof(HeightSliderValue));
        }

        if (excludeFromUpdate == null || !excludeFromUpdate.Contains(nameof(HeightEntryValue)))
        {
            _heightEntryValue = Height.ToString();
            OnPropertyChanged(nameof(HeightEntryValue));
        }
#pragma warning restore MVVMTK0034 // Direct field reference to [ObservableProperty] backing field
    }

    private void calculateAspectRatio()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        AspectRatioDouble = Width / Height;
        var gcd = greatestCommonDivisor((int)Width, (int)Height);
        AspectRatioString = $"{Width / gcd}:{Height / gcd}"; 
    }

    private int greatestCommonDivisor(int a, int b)
    {
        return b == 0 ? a : greatestCommonDivisor(b, a % b);
    }

    private void updateExampleRectangle()
    {
        var isPortrait = Width <= Height;
        
        if (isPortrait)
        {
            ExampleRectangleWidth = ExampleRectangleContainerWidth * AspectRatioDouble;
            ExampleRectangleHeight = ExampleRectangleContainerHeight;
        }
        else
        {
            ExampleRectangleWidth = ExampleRectangleContainerWidth;
            ExampleRectangleHeight = ExampleRectangleContainerHeight / AspectRatioDouble;
        }
    }
}
