using MobileDiffusion.Interfaces.ViewModels;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Models;
using System.Collections.ObjectModel;
using SkiaSharp.Views.Maui.Controls;
using SkiaSharp;
using CommunityToolkit.Maui.Alerts;

namespace MobileDiffusion.ViewModels;

public partial class MainPageViewModel : PageViewModel, IMainPageViewModel, IQueryAttributable
{
    private readonly IFileService _fileService;
    private readonly IStableDiffusionService _stableDiffusionService;
    private readonly IPopupService _popupService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IImageService _imageService;
    
    private Settings _settings = new();
    private string _resizedInitImage;
    private bool _initImageNeedsResize = true;
    private float _targetProgress = 0;
    private List<PromptDescriptor> _promptDescriptors = new();

    [ObservableProperty]
    private bool hasInitImage;

    [ObservableProperty]
    private bool hasPromptDescriptors;

    [ObservableProperty]
    private string prompt;

    [ObservableProperty]
    private string promptDescriptorsString;

    [ObservableProperty]
    private string placeholderPrompt = "An astronaut floating in space, detailed digital drawing, octane render, trending on artstation";

    [ObservableProperty]
    private float progress;

    [ObservableProperty]
    private ObservableCollection<IResultItemViewModel> results = new();

    public MainPageViewModel(
        IFileService fileService,
        IStableDiffusionService stableDiffusionService,
        IPopupService popupService,
        IServiceProvider serviceProvider,
        IImageService imageService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
    }

    [RelayCommand]
    private async Task Create()
    {
        if (!Preferences.Default.ContainsKey(Constants.PreferenceKeys.ServerUrl))
        {
            await Shell.Current.CurrentPage.DisplayAlert(
                "No server URL",
                "There is no server URL configured. Please set the server URL in app settings and try again.",
                "OK");

            return;
        }

        Progress = 0;

        Results = new();

        var settings = _settings.Clone();

        for (var i = 0; i < settings.NumOutputs; i++)
        {
            var resultItem = _serviceProvider.GetService<IResultItemViewModel>();

            resultItem.SetSettingsCommand = new RelayCommand<Settings>(setSettingsFromResultItem);
            resultItem.SetInitImageCommand = new RelayCommand<string>(setInitImageFromResultItem);
            resultItem.SetCanvasImageCommand = new AsyncRelayCommand<string>(setCanvasImageFromResultItem);

            Results.Add(resultItem);
        }

        if (settings.FitClientSide)
        {
            if (_initImageNeedsResize)
            {
                var imageString = await GetResizedImageStringFromSettingsAsync(settings);

                if (!string.IsNullOrEmpty(imageString))
                {
                    settings.InitImage = imageString;

                    _resizedInitImage = imageString;
                    _initImageNeedsResize = false;
                }
            }
            else
            {
                settings.InitImage = _resizedInitImage;
            }
        }

        var finalPrompt = (string.IsNullOrEmpty(Prompt) ? PlaceholderPrompt : Prompt);

        if (!string.IsNullOrEmpty(PromptDescriptorsString))
        {
            finalPrompt += $", {PromptDescriptorsString}";
        }

        settings.Prompt = finalPrompt;

        var sanitizedPrompt = finalPrompt.Replace(" ", "_").ToLower();
        var length = Math.Min(sanitizedPrompt.Length, 90);

        var imageNumber = 0;

        try
        {
            await foreach (var item in _stableDiffusionService.SubmitTextToImageRequest(settings))
            {
                if (item.Event.Equals("step", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(settings.InitImage))
                    {
                        _targetProgress = item.Step / (float)(settings.NumInferenceSteps);
                    }
                    else
                    {
                        _targetProgress = item.Step / (float)(settings.NumInferenceSteps * settings.PromptStrength);
                    }

                    Shell.Current.CurrentPage.AbortAnimation("ProgressAnimation");
                    
                    var animation = new Animation(value =>
                    {
                        Progress = (float)value;
                    }, Progress, _targetProgress, Easing.SinOut);

                    animation.Commit(Shell.Current.CurrentPage, "ProgressAnimation", length: 250);

                    continue;
                }

                var fileNameNoExtension = $"{sanitizedPrompt[..length]}-{item.Seed}-{DateTime.Now.Ticks}";

                if (item.Event.Equals("result", StringComparison.OrdinalIgnoreCase))
                {
                    var result = Results.FirstOrDefault(r => r.ResponseItem == null);

                    result.ResponseItem = item;

                    await retrieveResultImageAsync(result, fileNameNoExtension, imageNumber++);
                }
                else if (item.Event.Equals("upscaling-started", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var result in Results)
                    {
                        result.IsLoading = true;
                    }
                }
                else if (item.Event.Equals("upscaling-done", StringComparison.OrdinalIgnoreCase))
                {
                    foreach(var result in Results)
                    {
                        if (result != null)
                        {
                            await retrieveResultImageAsync(result, fileNameNoExtension, imageNumber++);
                        }
                    }                    
                }
            }
        }
        catch (System.Net.WebException webException)
        {
            // TODO - Handle this

            return;
        }
        catch (Exception e)
        {
            // TODO - Handle this

            return;
        }

        // Any remaining results that weren't set have failed
        foreach (var result in Results)
        {
            if (result.IsLoading)
            {
                result.IsLoading = false;
                result.Failed = true;
            }
        }
    }

    private async Task<string> GetResizedImageStringFromSettingsAsync(Settings settings)
    {
        if (string.IsNullOrEmpty(settings.InitImage) ||
            !settings.FitClientSide)
        {
            return string.Empty;
        }

        var tokenSource = new CancellationTokenSource();

        return await Task.Run(async () =>
        {
            var stream = await _imageService.GetStreamFromContentTypeStringAsync(settings.InitImage, tokenSource.Token);

            var bytes = _imageService.GetResizedImageStreamBytes(stream, (int)settings.Width, (int)settings.Height);

            if (bytes == null ||
                bytes.Length == 0)
            {
                return string.Empty;
            }

            var imageString = Convert.ToBase64String(bytes);

            return string.Format(Constants.ImageDataFormat, "image/png", imageString);
        }, tokenSource.Token);
    }

    private async Task retrieveResultImageAsync(IResultItemViewModel result, string fileName, int number)
    {
        var imageBytes = await _stableDiffusionService.GetImageBytesAsync(result.ResponseItem);

        var uri = await _fileService.WriteFileToInternalStorageAsync($"{fileName}-{number++}.png", imageBytes);

        result.InternalUri = uri;

        // Using a regular image source crashes the app quite often: https://github.com/dotnet/maui/issues/9712
        //result.ImageSource = ImageSource.FromFile(uri);

        // Use SkiaSharp's SKBitmapImageSource image source instead
        var imageSource = new SKBitmapImageSource
        {
            Bitmap = SKBitmap.Decode(imageBytes)
        };
        result.ImageSource = imageSource;

        result.IsLoading = false;
    }

    [RelayCommand]
    private async Task ShowAppSettings()
    {
        await Shell.Current.GoToAsync("AppSettingsPage");
    }

    [RelayCommand]
    private async Task ShowImageToImageSettings()
    {
        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        await Shell.Current.GoToAsync("ImageToImageSettingsPage", parameters);
    }

    [RelayCommand]
    private async Task ShowPromptDescriptors()
    {
        var parameters = new Dictionary<string, object> { { NavigationParams.PromptDescriptors, _promptDescriptors } };

        await Shell.Current.GoToAsync("PromptDescriptorsPage", parameters);
    }

    [RelayCommand]
    private async Task ShowRequestSettings()
    {
        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        await Shell.Current.GoToAsync("PromptSettingsPage", parameters);
    }

    public override async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(NavigationParams.PromptSettings, out var promptSettings) &&
            promptSettings is Settings settings)
        {
            _settings = settings;

            updateHasInitImage();
        }

        if (query.TryGetValue(NavigationParams.InitImgString, out var initImagParam))
        {
            _settings.InitImage = initImagParam as string;

            updateHasInitImage();
        }

        if (query.TryGetValue(NavigationParams.ImageWidth, out var imageWidthParam) &&
            imageWidthParam is float imageWidth)
        {
            _settings.Width = imageWidth;
        }

        if (query.TryGetValue(NavigationParams.ImageHeight, out var imageHeightParam) &&
            imageHeightParam is float imageHeight)
        {
            _settings.Height = imageHeight;
        }

        if (query.TryGetValue(NavigationParams.AppShareFileUri, out var appShareFileUriParam) &&
            appShareFileUriParam is string imageUri)
        {
            query.TryGetValue(NavigationParams.AppShareContentType, out var appShareContentTypeParam);

            await LoadSharedImage(imageUri, appShareContentTypeParam as string);
        }

        if (query.TryGetValue(NavigationParams.PromptDescriptors, out var promptDescriptorsParam) &&
            promptDescriptorsParam is List<PromptDescriptor> promptDescriptors)
        {
            _promptDescriptors = promptDescriptors;

            PromptDescriptorsString = string.Join(", ", _promptDescriptors.Select(d => d.Text));

            HasPromptDescriptors = _promptDescriptors.Any();
        }

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    private async Task LoadSharedImage(string imageUri, string contentType)
    {
        var useAsSourceImage = !await Shell.Current.DisplayAlert(
                "Where to?",
                "Would you like to use the image as a source image or put it in the canvas for masking?",
                "Canvas",
                "Source Image");

        if (useAsSourceImage)
        {
            await SetSourceImageFromUri(imageUri, contentType);
        }
        else
        {
            var parameters = new Dictionary<string, object>
            {
                { NavigationParams.AppShareFileUri, imageUri },
                { NavigationParams.AppShareContentType, contentType }
            };

            await Shell.Current.GoToAsync("///MaskPageTab", parameters);
        }
    }

    private async Task SetSourceImageFromUri(string imageUri, string contentType)
    {
        try
        {
            using var stream = await _fileService.GetFileStreamUsingExactUriAsync(imageUri);

            if (stream == null)
            {
                Console.WriteLine("Stream is null");
                throw new FileNotFoundException(imageUri);
            }

            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            var imageBytes = memoryStream.ToArray();
            var imageString = Convert.ToBase64String(imageBytes);

            var formattedImageString = string.Format(Constants.ImageDataFormat, contentType ?? "image/png", imageString);

            _settings.InitImage = formattedImageString;
        }
        catch
        {
            await Toast.Make("Unable to load requested image. Please try again.").Show();
            return;
        }
        finally
        {
            updateHasInitImage();
        }
    }

    private void setSettingsFromResultItem(Settings settings)
    {
        if (settings == null)
        {
            return;
        }

        var initImage = string.Empty;

        if (string.IsNullOrEmpty(settings.InitImage) &&
            !string.IsNullOrEmpty(_settings.InitImage))
        {
            initImage = _settings.InitImage;
        }

        _settings = settings;

        if (!string.IsNullOrEmpty(initImage))
        {
            _settings.InitImage = initImage;
        }

        updateHasInitImage();
    }

    private void setInitImageFromResultItem(string initImage)
    {
        if (string.IsNullOrEmpty(initImage))
        {
            return;
        }

        _settings.InitImage = initImage;

        updateHasInitImage();
    }

    private async Task setCanvasImageFromResultItem(string canvasImage)
    {
        if (string.IsNullOrEmpty(canvasImage))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
            {
                {NavigationParams.CanvasImageString, canvasImage}
            };

        await Shell.Current.GoToAsync("///MaskPageTab", parameters);
    }

    private void updateHasInitImage()
    {
        HasInitImage = !string.IsNullOrEmpty(_settings?.InitImage);

        if (HasInitImage)
        {
            _initImageNeedsResize = true;
        }
    }

    public override void OnAppearing()
    {
        // Possible workaround for the following bugs:
        // https://github.com/dotnet/maui/issues/9011
        // https://github.com/dotnet/maui/issues/8809
        // However, it doesn't work because of this bug:
        // https://github.com/dotnet/maui/issues/8787

        //refreshImageSources();
    }

    public override void OnDisappearing()
    {
        // Possible workaround for the following bugs:
        // https://github.com/dotnet/maui/issues/9011
        // https://github.com/dotnet/maui/issues/8809
        // However, it doesn't work because of this bug:
        // https://github.com/dotnet/maui/issues/8787

        //clearImageSources();
    }

    private void clearImageSources()
    {
        foreach (var result in Results)
        {
            result.ImageSource = default(ImageSource);
        }
    }

    private void refreshImageSources()
    {
        foreach (var result in Results)
        {
            if (string.IsNullOrEmpty(result.InternalUri))
            {
                continue;
            }

            result.ImageSource = ImageSource.FromFile(result.InternalUri);
        }
    }
}
