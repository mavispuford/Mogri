using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Helpers;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Interfaces.ViewModels.Popups;

namespace MobileDiffusion.ViewModels;

public partial class ResolutionSelectPopupViewModel : PopupBaseViewModel, IResolutionSelectPopupViewModel
{
    private readonly IImageService _imageService;
    private string? _initImgString;

    [ObservableProperty]
    public partial double AspectRatioDouble { get; set; }

    [ObservableProperty]
    public partial string? AspectRatioString { get; set; }

    [ObservableProperty]
    public partial string? AspectRatioEntryValue { get; set; }

    [ObservableProperty]
    public partial double Width { get; set; }

    [ObservableProperty]
    public partial double Height { get; set; }

    [ObservableProperty]
    public partial double WidthSliderValue { get; set; }

    [ObservableProperty]
    public partial double HeightSliderValue { get; set; }

    [ObservableProperty]
    public partial string? WidthEntryValue { get; set; }

    [ObservableProperty]
    public partial string? HeightEntryValue { get; set; }

    [ObservableProperty]
    public partial bool PreserveAspectRatio { get; set; } = true;

    [ObservableProperty]
    public partial ImageSource? InitImageSource { get; set; }

    [ObservableProperty]
    public partial double ExampleRectangleContainerWidth { get; set; }

    [ObservableProperty]
    public partial double ExampleRectangleContainerHeight { get; set; }

    [ObservableProperty]
    public partial double ExampleRectangleWidth { get; set; }

    [ObservableProperty]
    public partial double ExampleRectangleHeight { get; set; }

    [ObservableProperty]
    public partial double MinimumWidthHeight { get; set; } = Constants.MinimumWidthHeight;

    [ObservableProperty]
    public partial double MaximumWidthHeight { get; set; } = Constants.MaximumWidthHeight;

    private bool _isUpdating;

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

        if (InitImageSource == null && !string.IsNullOrEmpty(_initImgString))
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

    partial void OnAspectRatioEntryValueChanged(string? value)
    {
        if (_isUpdating) return;

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
        if (_isUpdating) return;
        updateWidth(value);
    }

    partial void OnHeightSliderValueChanged(double value)
    {
        if (_isUpdating) return;
        updateHeight(value);
    }

    partial void OnWidthEntryValueChanged(string? value)
    {
        if (_isUpdating) return;
        if (double.TryParse(value, out double width))
        {
            updateWidth(width, excludeFromUpdate: nameof(WidthEntryValue));
        }
    }

    partial void OnHeightEntryValueChanged(string? value)
    {
        if (_isUpdating) return;
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
        _isUpdating = true;
        try
        {
            if (excludeFromUpdate == null || !excludeFromUpdate.Contains(nameof(AspectRatioEntryValue)))
            {
                AspectRatioEntryValue = AspectRatioString;
            }

            if (excludeFromUpdate == null || !excludeFromUpdate.Contains(nameof(WidthSliderValue)))
            {
                WidthSliderValue = Width;
            }

            if (excludeFromUpdate == null || !excludeFromUpdate.Contains(nameof(WidthEntryValue)))
            {
                WidthEntryValue = Width.ToString();
            }

            if (excludeFromUpdate == null || !excludeFromUpdate.Contains(nameof(HeightSliderValue)))
            {
                HeightSliderValue = Height;
            }

            if (excludeFromUpdate == null || !excludeFromUpdate.Contains(nameof(HeightEntryValue)))
            {
                HeightEntryValue = Height.ToString();
            }
        }
        finally
        {
            _isUpdating = false;
        }
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
