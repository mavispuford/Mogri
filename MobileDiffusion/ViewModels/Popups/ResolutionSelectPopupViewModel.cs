using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Helpers;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class ResolutionSelectPopupViewModel : PopupBaseViewModel, IResolutionSelectPopupViewModel
{
    private readonly IImageService _imageService;
    private string _initImgString;

    [ObservableProperty]
    private double _aspectRatioDouble;

    [ObservableProperty]
    private string _aspectRatioString;

    [ObservableProperty]
    private string _aspectRatioEntryValue;

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
    private double _minimumWidthHeight = Constants.MinimumWidthHeight;

    [ObservableProperty]
    private double _maximumWidthHeight = Constants.MaximumWidthHeight;

    public ResolutionSelectPopupViewModel(IPopupService popupService,
        IImageService imageService) : base(popupService)
    {
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
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

        if (query.TryGetValue(NavigationParams.InitImgString, out var initImgParam) &&
            initImgParam is string initImgString &&
            !string.IsNullOrEmpty(initImgString))
        {
            _initImgString = initImgString;
        }

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();

        var aspectRatioResult = MathHelper.CalculateAspectRatio(Width, Height);

        AspectRatioDouble = aspectRatioResult.AspectRatioDouble;
        AspectRatioString = aspectRatioResult.AspectRatioString;
        
        UpdateAllValues();
    }

    public override async Task OnAppearingAsync()
    {
        await base.OnAppearingAsync();

        if (InitImageSource == null)
        {
            var initCancellationTokenSource = new CancellationTokenSource();

            InitImageSource = await _imageService.GetImageSourceFromContentTypeStringAsync(_initImgString, initCancellationTokenSource.Token);
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
        var parameters = new Dictionary<string, object>
        {
            { NavigationParams.Width, Width },
            { NavigationParams.Height, Height }
        };

        await ClosePopupAsync(parameters);
    }
    
    partial void OnExampleRectangleContainerWidthChanged(double value)
    {
        updateExampleRectangle();
    }

    partial void OnExampleRectangleContainerHeightChanged(double value)
    {
        updateExampleRectangle();
    }

    partial void OnAspectRatioEntryValueChanged(string value)
    {
        if (string.IsNullOrEmpty(value) || value == AspectRatioString)
        {
            return;
        }

        var splitValue = value.Split(":");

        if (splitValue.Length != 2 || 
            !int.TryParse(splitValue[0], out int aspectWidth) ||
            aspectWidth <= 0 ||
            !int.TryParse(splitValue[1], out int aspectHeight) ||
            aspectHeight <= 0)
        {
            return;
        }

        var constrainedDimensions = MathHelper.GetAspectCorrectConstrainedDimensions(Width, Height, aspectWidth, aspectHeight, MathHelper.DimensionConstraint.ClosestMatch);

        var aspectRatioResult = MathHelper.CalculateAspectRatio(constrainedDimensions.Width, constrainedDimensions.Height);

        AspectRatioDouble = aspectRatioResult.AspectRatioDouble;
        AspectRatioString = aspectRatioResult.AspectRatioString;

        Width = constrainedDimensions.Width;
        Height = constrainedDimensions.Height;

        UpdateAllValues(nameof(AspectRatioEntryValue));
        updateExampleRectangle();
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
        var roundedWidth = MathHelper.ConstrainDimensionValue(value);

        if (PreserveAspectRatio && !aspectCorrectedValue)
        {
            var calculatedHeight = roundedWidth / AspectRatioDouble;
            var roundedHeight = MathHelper.ConstrainDimensionValue(calculatedHeight);
            var ratio = roundedWidth / roundedHeight;

            if (roundedHeight < MinimumWidthHeight || roundedHeight > MaximumWidthHeight ||
                Math.Abs(AspectRatioDouble - ratio) > .005d)
            {
                return;
            }
        }

        Width = roundedWidth;

        if (!aspectCorrectedValue)
        {
            if (PreserveAspectRatio)
            {
                var calculatedHeight = Width / AspectRatioDouble;

                updateHeight(calculatedHeight, true);
            }
            else
            {
                var aspectRatioResult = MathHelper.CalculateAspectRatio(Width, Height);

                AspectRatioDouble = aspectRatioResult.AspectRatioDouble;
                AspectRatioString = aspectRatioResult.AspectRatioString;
            }

            UpdateAllValues(excludeFromUpdate);
        }

        updateExampleRectangle();
    }

    private void updateHeight(double value, bool aspectCorrectedValue = false, params string[] excludeFromUpdate)
    {
        var roundedHeight = MathHelper.ConstrainDimensionValue(value);

        if (PreserveAspectRatio && !aspectCorrectedValue)
        {
            var calculatedWidth = roundedHeight * AspectRatioDouble;
            var roundedWidth = MathHelper.ConstrainDimensionValue(calculatedWidth);
            var ratio = roundedWidth / roundedHeight;

            if (roundedWidth < MinimumWidthHeight || roundedWidth > MaximumWidthHeight ||
                Math.Abs(AspectRatioDouble - ratio) > .005d)
            {
                return;
            }
        }

        Height = roundedHeight;

        if (!aspectCorrectedValue)
        {
            if (PreserveAspectRatio)
            {
                var calculatedWidth = Height * AspectRatioDouble;

                updateWidth(calculatedWidth, true);
            }
            else
            {
                var aspectRatioResult = MathHelper.CalculateAspectRatio(Width, Height);

                AspectRatioDouble = aspectRatioResult.AspectRatioDouble;
                AspectRatioString = aspectRatioResult.AspectRatioString;
            }

            UpdateAllValues(excludeFromUpdate);
        }

        updateExampleRectangle();
    }

    [RelayCommand]
    private void UpdateAllValues(params string[] excludeFromUpdate)
    {
#pragma warning disable MVVMTK0034 // Direct field reference to [ObservableProperty] backing field

        if (excludeFromUpdate == null || !excludeFromUpdate.Contains(nameof(AspectRatioEntryValue)))
        {
            _aspectRatioEntryValue = AspectRatioString;
            OnPropertyChanged(nameof(AspectRatioEntryValue));
        }

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

    private void updateExampleRectangle()
    {
        if (ExampleRectangleContainerWidth == -1 || ExampleRectangleContainerHeight == -1)
        {
            return;
        }

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

        // Broadcast every time since there are times where the view doesn't look right
        OnPropertyChanged(nameof(ExampleRectangleWidth));
        OnPropertyChanged(nameof(ExampleRectangleHeight));
    }
}
