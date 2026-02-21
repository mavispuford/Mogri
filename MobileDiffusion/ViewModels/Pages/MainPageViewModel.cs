using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Interfaces.ViewModels.Pages;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Models;
using System.Collections.ObjectModel;
using SkiaSharp.Views.Maui.Controls;
using CommunityToolkit.Maui.Alerts;

namespace MobileDiffusion.ViewModels;

public partial class MainPageViewModel : PageViewModel, IMainPageViewModel
{
    const string _defaultPrompt = "An astronaut floating in space, detailed digital drawing, octane render, trending on artstation";

    private readonly IFileService _fileService;
    private readonly IImageGenerationService _stableDiffusionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IImageService _imageService;

    private PromptSettings _settings = new();
    private string? _resizedInitImage;
    private string? _resizedMaskImage;
    private bool _initImageNeedsResize = true;
    private bool _forceReinitialize;
    private float _targetProgress = 0;

    [ObservableProperty]
    public partial bool HasInitImage { get; set; }

    [ObservableProperty]
    public partial string? Prompt { get; set; } = _defaultPrompt;

    [ObservableProperty]
    public partial string? NegativePrompt { get; set; }

    [ObservableProperty]
    public partial float Progress { get; set; }

    [ObservableProperty]
    public partial bool ServerConnected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCancelButton))]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCancelButton))]
    public partial BackendCapabilities CurrentCapabilities { get; set; } = BackendCapabilities.None;

    public bool ShowCancelButton => IsGenerating && CurrentCapabilities.SupportsCancellation;

    [ObservableProperty]
    public partial ObservableCollection<IResultItemViewModel> Results { get; set; } = new();

    public MainPageViewModel(
        IFileService fileService,
        IImageGenerationService stableDiffusionService,
        IServiceProvider serviceProvider,
        IImageService imageService,
        ILoadingService loadingService) : base(loadingService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();

        await initializeStableDiffusionService();
    }

    private async Task<bool> initializeStableDiffusionService()
    {
        if (_stableDiffusionService.Initialized && !_forceReinitialize)
        {
            ServerConnected = true;
            return true;
        }

        _forceReinitialize = false;

        try
        {
            await LoadingService.ShowAsync("Connecting...");

            await _stableDiffusionService.InitializeAsync();

            if (!_stableDiffusionService.Initialized)
            {
                ServerConnected = false;
                return false;
            }

            var samplers = await _stableDiffusionService.GetSamplersAsync();

            var profile = GenerationProfile.GetDefault(Enums.ModelType.SDXL);
            
            // Preserve current sampler if it exists in the new list (e.g. switching backends)
            var currentSampler = _settings.Sampler;
            var currentScheduler = _settings.Scheduler;
            
            _settings.Steps = profile.DefaultSteps;
            _settings.GuidanceScale = profile.DefaultCfg;
            _settings.Width = profile.DefaultWidth;
            _settings.Height = profile.DefaultHeight;

            if (samplers != null)
            {
                if (!string.IsNullOrEmpty(currentSampler) && samplers.ContainsKey(currentSampler))
                {
                    _settings.Sampler = currentSampler;
                }
                else if (samplers.ContainsKey(profile.DefaultSampler))
                {
                    _settings.Sampler = profile.DefaultSampler;
                }
                else
                {
                    _settings.Sampler = samplers.FirstOrDefault().Key ?? "Euler";
                }
            }
            else
            {
                _settings.Sampler = profile.DefaultSampler;
            }

            var schedulers = await _stableDiffusionService.GetSchedulersAsync();

            if (schedulers != null && schedulers.Any())
            {
                if (!string.IsNullOrEmpty(currentScheduler) && schedulers.Contains(currentScheduler))
                {
                    _settings.Scheduler = currentScheduler;
                }
                else if (!string.IsNullOrEmpty(profile.DefaultScheduler) && schedulers.Contains(profile.DefaultScheduler))
                {
                    _settings.Scheduler = profile.DefaultScheduler;
                }
                else
                {
                    _settings.Scheduler = schedulers.FirstOrDefault();
                }
            }
            else
            {
                _settings.Scheduler = profile.DefaultScheduler;
            }

            _settings.Model = await _stableDiffusionService.GetSelectedModelAsync();
        }
        catch
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync(
                "Connection problems",
                "Unable to connect to the configured server URL. Please double check your app settings/connectivity and try again.",
                "OK");

            ServerConnected = false;
            return false;
        }
        finally
        {
            await LoadingService.HideAsync();
        }

        ServerConnected = true;
        CurrentCapabilities = _stableDiffusionService.Capabilities;
        return true;
    }

    [RelayCommand]
    private async Task Create()
    {
        if (!Preferences.Default.ContainsKey(Constants.PreferenceKeys.ServerUrl))
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync(
                "No server URL",
                "There is no server URL configured. Please set the server URL in app settings and try again.",
                "OK");

            return;
        }

        if (!await initializeStableDiffusionService())
        {
            return;
        }

        if (!await _stableDiffusionService.CheckServerAsync())
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync(
                "Connection Problems",
                "Unable to connect to the server. Please verify your connectivity and try again.",
                "OK");

            return;
        }

        DeviceDisplay.Current.KeepScreenOn = true;

        try
        {
            IsGenerating = true;
            Progress = 0;

            Results = new();

            var settings = _settings.Clone();

            if (settings == null) return;

            for (var i = 0; i < settings.BatchCount * settings.BatchSize; i++)
            {
                var resultItem = _serviceProvider.GetService<IResultItemViewModel>();

                if (resultItem == null) continue;

                resultItem.ParentCollection = Results;
                resultItem.ApplyQueryParamsFromResultItemCommand = new RelayCommand<IDictionary<string, object>>(ApplyQueryAttributes);

                Results.Add(resultItem);
            }

            if (HasInitImage && settings.FitClientSide)
            {
                if (_initImageNeedsResize)
                {
                    var initImageResult = await GetResizedImageStringFromSettingsAsync(settings, settings.InitImage ?? string.Empty, filterImage: true);

                    if (!string.IsNullOrEmpty(initImageResult.ImageString))
                    {
                        settings.InitImage = initImageResult.ImageString;

                        _resizedInitImage = initImageResult.ImageString;
                        _initImageNeedsResize = false;
                    }

                    if (!string.IsNullOrEmpty(settings.Mask))
                    {
                        var maskImageResult = await GetResizedImageStringFromWidthAndHeightAsync(initImageResult.ActualWidth, initImageResult.ActualHeight, settings.Mask, true, true);

                        if (!string.IsNullOrEmpty(maskImageResult.ImageString))
                        {
                            settings.Mask = maskImageResult.ImageString;

                            _resizedMaskImage = maskImageResult.ImageString;
                        }
                    }
                }
                else
                {
                    settings.InitImage = _resizedInitImage;
                    settings.Mask = _resizedMaskImage;
                }
            }

            settings.Prompt = string.IsNullOrEmpty(settings.Prompt) ? _defaultPrompt : settings.Prompt;

            var sanitizedPrompt = settings.Prompt.Replace(" ", "_").ToLower();
            var length = Math.Min(sanitizedPrompt.Length, 90);

            var imageNumber = 0;

            try
            {
                await foreach (var response in _stableDiffusionService.SubmitImageRequestAsync(settings))
                {
                    // Some servers surface progress as a numeric Progress or a ProgressResponse object.
                    if (response.Progress > 0)
                    {
                        reportProgress((float)response.Progress);
                    }

                    if (response.ResponseObject is ProgressResponse progressResponse)
                    {
                        reportProgress((float)progressResponse.Progress);
                    }
                    else if (response.ResponseObject is GenerationResponse generationResponse)
                    {
                        foreach (var image in generationResponse.Images ?? Enumerable.Empty<string>())
                        {
                            var currentSeed = generationResponse.Seeds?.ElementAtOrDefault(imageNumber) ?? settings.Seed + imageNumber;
                            var fileNameNoExtension = $"{sanitizedPrompt[..length]}-{currentSeed}-{DateTime.Now.Ticks}";

                            var result = Results.FirstOrDefault(r => r.ApiResponse == null);

                            if (result != null)
                            {
                                result.ApiResponse = response;
                                result.Settings = settings.Clone();
                                result.Settings.Seed = currentSeed;

                                await retrieveResultImageAsync(result, fileNameNoExtension, imageNumber++);
                            }
                        }
                    }
                }
            }
            catch (System.Net.Sockets.SocketException socketException)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Connection Error", $"A network error occurred: {socketException.Message}", "OK");
            }
            catch (System.Net.WebException webException)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Web Error", $"A web error occurred: {webException.Message}", "OK");
            }
            catch (Exception e)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Error", $"An unexpected error occurred: {e.Message}", "OK");
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

            vibrate(HapticFeedbackType.LongPress);
        }
        finally
        {
            IsGenerating = false;
            DeviceDisplay.Current.KeepScreenOn = false;
        }
    }

    private void reportProgress(float progress)
    {
        _targetProgress = progress;

        Shell.Current.CurrentPage.AbortAnimation("ProgressAnimation");

        var animation = new Animation(value =>
        {
            Progress = (float)value;
        }, Progress, _targetProgress, Easing.SinOut);

        animation.Commit(Shell.Current.CurrentPage, "ProgressAnimation", length: 500);
    }

    private Task<(string ImageString, int ActualWidth, int ActualHeight)> GetResizedImageStringFromSettingsAsync(PromptSettings settings, string sourceImageString, bool forceExactSize = false, bool filterImage = false)
    {
        if (string.IsNullOrEmpty(sourceImageString) ||
            !settings.FitClientSide)
        {
            return Task.FromResult((string.Empty, 0, 0));
        }

        return GetResizedImageStringFromWidthAndHeightAsync((int)settings.Width, (int)settings.Height, sourceImageString, forceExactSize);
    }

    private Task<(string ImageString, int ActualWidth, int ActualHeight)> GetResizedImageStringFromWidthAndHeightAsync(int width, int height, string sourceImageString, bool forceExactSize = false, bool filterImage = false)
    {
        if (string.IsNullOrEmpty(sourceImageString))
        {
            return Task.FromResult((string.Empty, 0, 0));
        }

        var tokenSource = new CancellationTokenSource();

        return Task.Run(async () =>
        {
            var stream = await _imageService.GetStreamFromContentTypeStringAsync(sourceImageString, tokenSource.Token);

            var result = _imageService.GetResizedImageStreamBytes(stream, width, height, forceExactSize, filterImage);

            if (result.Bytes == null ||
                result.Bytes.Length == 0)
            {
                return (string.Empty, 0, 0);
            }

            var imageString = Convert.ToBase64String(result.Bytes);

            return (string.Format(Constants.ImageDataFormat, "image/png", imageString), result.ActualWidth, result.ActualHeight);
        }, tokenSource.Token);
    }

    private async Task retrieveResultImageAsync(IResultItemViewModel result, string fileName, int number)
    {
        byte[]? imageBytes = null;

        if (result.ApiResponse.ResponseObject is GenerationResponse generationResponse && generationResponse.Images != null)
        {
            var imageString = generationResponse.Images.ElementAtOrDefault(number);
            if (imageString != null)
            {
                imageBytes = Convert.FromBase64String(imageString);
                if (result.Settings != null)
                {
                    imageBytes = MobileDiffusion.Helpers.PngMetadataHelper.WriteSettings(imageBytes, result.Settings);
                }
            }
        }

        if (imageBytes == null) return;

        var uri = await _fileService.WriteFileToInternalStorageAsync($"{fileName}-{number++}.png", imageBytes);

        result.InternalUri = uri;

        // Using a regular image source causes them to disappear when returning to the page: https://github.com/dotnet/maui/issues/15669
        // ** They also don't seem to work with larger image files **
        //result.ImageSource = ImageSource.FromFile(uri);

        // Resize if too large
        using var fileStream = await _fileService.GetFileStreamFromInternalStorageAsync(uri);
        var bitmap = _imageService.GetSkBitmapFromStream(fileStream);
        bitmap = _imageService.GetResizedSKBitmap(bitmap, (int)Constants.MaximumDisplayWidthHeight, (int)Constants.MaximumDisplayWidthHeight, filterImage: true, onlyIfLarger: true);

        // Use SkiaSharp's SKBitmapImageSource image source instead
        var imageSource = new SKBitmapImageSource
        {
            Bitmap = bitmap
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
    private async Task ShowHistory()
    {
        await Shell.Current.GoToAsync("HistoryPage");
    }

    [RelayCommand]
    private async Task Cancel()
    {
        try
        {
            await _stableDiffusionService.CancelAsync();
        }
        catch
        {
            // Ignore
        }
    }

    [RelayCommand]
    private async Task ShowImageToImageSettings()
    {
        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        await Shell.Current.GoToAsync("ImageToImageSettingsPage", parameters);
    }

    [RelayCommand]
    private async Task ShowPromptPage()
    {
        var parameters = new Dictionary<string, object> {
            { NavigationParams.PromptPlaceholder, _defaultPrompt },
            { NavigationParams.PromptSettings, _settings },
        };

        await Shell.Current.GoToAsync("PromptPage", parameters);
    }

    [RelayCommand]
    private async Task ShowPromptSettings()
    {
        if (!_stableDiffusionService.Initialized)
        {
            await LoadingService.ShowAsync("Initializing...");

            try
            {
                if (!await initializeStableDiffusionService())
                {
                    return;
                }
            }
            finally
            {
                await LoadingService.HideAsync();
            }
        }

        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        await Shell.Current.GoToAsync("PromptSettingsPage", parameters);
    }

    public override async void ApplyQueryAttributes(IDictionary<string, object>? query)
    {
        if (query == null) return;

        if (query.ContainsKey(NavigationParams.ForceReinitialize))
        {
            _forceReinitialize = true;
        }

        if (query.TryGetValue(NavigationParams.PromptSettings, out var promptSettings) &&
            promptSettings is PromptSettings settings)
        {
            // Validate that the model/resources exist in the current backend
            _ = validateSettingsResourcesAsync(settings);

            _settings = settings;

            Prompt = string.IsNullOrEmpty(settings.Prompt) ? _defaultPrompt : settings.Prompt;
            NegativePrompt = settings.NegativePrompt;

            updateHasInitImage();
        }

        if (query.TryGetValue(NavigationParams.InitImgString, out var initImageParam))
        {
            _settings.InitImage = initImageParam as string;
            _resizedInitImage = null;
            _settings.Mask = null;
            _resizedMaskImage = null;

            if (query.TryGetValue(NavigationParams.InitImgThumbnail, out var initImageThumbnailParam))
            {
                _settings.InitImageThumbnail = initImageThumbnailParam as string;
            }
            else
            {
                _settings.InitImageThumbnail = null;
            }

            updateHasInitImage();
        }

        if (query.TryGetValue(NavigationParams.MaskImgString, out var maskImageParam))
        {
            _settings.Mask = maskImageParam as string;

            updateHasInitImage();
        }

        if (query.TryGetValue(NavigationParams.CanvasImageString, out var canvasImageParam))
        {
            var imageString = canvasImageParam as string;

            if (imageString != null)
            {
                var parameters = new Dictionary<string, object>
                {
                    {NavigationParams.CanvasImageString, imageString}
                };

                await Shell.Current.Dispatcher.DispatchAsync(async () =>
                {
                    await Shell.Current.Navigation.PopToRootAsync();
                    await Shell.Current.GoToAsync("///CanvasPageTab", parameters);
                });
            }
        }

        double? requestedWidth = null, requestedHeight = null;

        if (query.TryGetValue(NavigationParams.ImageWidth, out var imageWidthParam))
        {
            if (imageWidthParam is float imageWidthFloat)
            {
                requestedWidth = imageWidthFloat;
            }
            else if (imageWidthParam is double imageWidthDouble)
            {
                requestedWidth = imageWidthDouble;
            }
        }

        if (query.TryGetValue(NavigationParams.ImageHeight, out var imageHeightParam))
        {
            if (imageHeightParam is float imageHeightFloat)
            {
                requestedHeight = imageHeightFloat;
            }
            else if (imageHeightParam is double imageHeightDouble)
            {
                requestedHeight = imageHeightDouble;
            }
        }

        if (requestedWidth != null && requestedHeight != null)
        {
            if (requestedWidth.Value != _settings.Width ||
                requestedHeight.Value != _settings.Height)
            {
                var resChangeMessage = $"Would you like keep the resolution at {_settings.Width}x{_settings.Height} or CHANGE it to {requestedWidth.Value}x{requestedHeight.Value}?";
                var resChangeResult = await Shell.Current.DisplayAlertAsync("Confirm Resolution Change", resChangeMessage, "CHANGE", "Keep");

                if (resChangeResult)
                {
                    _settings.Width = requestedWidth.Value;
                    _settings.Height = requestedHeight.Value;
                }
            }
        }

        if (query.TryGetValue(NavigationParams.Seed, out var seedParam) &&
            seedParam is long seed)
        {
            _settings.BatchSize = 1;
            _settings.BatchCount = 1;
            _settings.Seed = seed;
        }

        if (query.TryGetValue(NavigationParams.AppShareFileUri, out var appShareFileUriParam) &&
            appShareFileUriParam is string imageUri)
        {
            query.TryGetValue(NavigationParams.AppShareContentType, out var appShareContentTypeParam);

            await LoadSharedImage(imageUri, appShareContentTypeParam as string ?? "image/png");
        }

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    private async Task validateSettingsResourcesAsync(PromptSettings settings)
    {
        try 
        {
            // Only validate if we are connected, otherwise getting models/loras might fail or try to init unnecessarily
            if (!_stableDiffusionService.Initialized && !await _stableDiffusionService.CheckServerAsync()) return;

            var messages = new List<string>();
            
            // Validate Model
            if (settings.Model != null)
            {
                var models = await _stableDiffusionService.GetModelsAsync();
                if (models != null && !models.Any(m => m.Key == settings.Model.Key))
                {
                     messages.Add($"Model: {settings.Model.DisplayName}");
                }
            }

            // Validate Loras
            if (settings.Loras != null && settings.Loras.Any())
            {
                var loras = await _stableDiffusionService.GetLorasAsync();
                if (loras != null) 
                {
                    foreach (var lora in settings.Loras)
                    {
                        if (!loras.Any(l => l.Name == lora.Name))
                        {
                            messages.Add($"LoRA: {lora.Name}");
                        }
                    }
                }
            }

            if (messages.Any())
            {
                var list = string.Join("\n", messages);
                await Shell.Current.DisplayAlertAsync("Missing Resources", 
                    $"The following resources are missing from the current backend:\n\n{list}\n\nGeneration may look different or fail.", 
                    "OK");
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error validating resources: {ex}");
        }
    }

    private async Task LoadSharedImage(string imageUri, string contentType)
    {
        var useAsSourceImage = !await Shell.Current.DisplayAlertAsync(
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

            await Shell.Current.GoToAsync("///CanvasPageTab", parameters);
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

            memoryStream.Seek(0, SeekOrigin.Begin);
            var bitmap = _imageService.GetSkBitmapFromStream(memoryStream);

            if (bitmap != null)
            {
                _settings.InitImageThumbnail = _imageService.GetThumbnailString(bitmap, contentType ?? "image/png");
            }
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

    private void updateHasInitImage()
    {
        HasInitImage = !string.IsNullOrEmpty(_settings?.InitImage);
        _initImageNeedsResize = true;
    }

    private void vibrate(HapticFeedbackType type)
    {
        if (HapticFeedback.Default.IsSupported)
        {
            HapticFeedback.Default.Perform(type);
        }
    }
}
