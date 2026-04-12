using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Mogri.Models;
using System.Collections.ObjectModel;
using SkiaSharp.Views.Maui.Controls;
using CommunityToolkit.Maui.Alerts;

namespace Mogri.ViewModels;

public partial class MainPageViewModel : PageViewModel, IMainPageViewModel
{
    const string _defaultPrompt = "Photo of a lone tree on a hill, golden hour";

    private readonly IFileService _fileService;
    private readonly IImageGenerationService _stableDiffusionService;
    private readonly IGenerationTaskService _generationTaskService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IImageService _imageService;
    private readonly IPopupService _popupService;
    private readonly ICheckpointSettingsService _checkpointSettingsService;

    private PromptSettings _settings = new();
    private string? _resizedInitImage;
    private string? _resizedMaskImage;
    private bool _initImageNeedsResize = true;
    private bool _forceReinitialize;
    private float _targetProgress = 0;
#if ANDROID
    private static bool _notificationPermissionRequested;
#endif

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
        IGenerationTaskService generationTaskService,
        IServiceProvider serviceProvider,
        IImageService imageService,
        IPopupService popupService,
        ICheckpointSettingsService checkpointSettingsService,
        ILoadingService loadingService) : base(loadingService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
        _generationTaskService = generationTaskService ?? throw new ArgumentNullException(nameof(generationTaskService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        _checkpointSettingsService = checkpointSettingsService ?? throw new ArgumentNullException(nameof(checkpointSettingsService));
    }

    public override async Task OnNavigatedToAsync()
    {
        _generationTaskService.ProgressChanged += onGenerationProgressChanged;
        _generationTaskService.Completed += onGenerationCompleted;

        await base.OnNavigatedToAsync();

        if (_generationTaskService.IsRunning)
        {
            IsGenerating = true;
        }
        else if (_generationTaskService.LastResult != null)
        {
            // Process the result that completed while we were away
            onGenerationCompleted(this, _generationTaskService.LastResult);
        }

        await initializeStableDiffusionService();
    }

    public override async Task OnNavigatedFromAsync()
    {
        _generationTaskService.ProgressChanged -= onGenerationProgressChanged;
        _generationTaskService.Completed -= onGenerationCompleted;

        await base.OnNavigatedFromAsync();
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

            var currentModelType = await _stableDiffusionService.GetCurrentModelTypeAsync();
            _settings.ModelType = currentModelType;
            var profile = GenerationProfile.GetDefault(currentModelType);

            var selectedModel = await _stableDiffusionService.GetSelectedModelAsync();
            _settings.Model = selectedModel;

            CheckpointSettings? persisted = null;
            if (selectedModel != null && !string.IsNullOrEmpty(selectedModel.Key))
            {
                persisted = _checkpointSettingsService.Load(selectedModel.Key);
            }

            if (persisted != null)
            {
                persisted.ApplyTo(_settings);
            }
            else
            {
                _settings.Steps = profile.DefaultSteps;
                _settings.GuidanceScale = profile.DefaultCfg;
                _settings.DistilledCfgScale = profile.DefaultDistilledCfg;
                _settings.Width = profile.DefaultWidth;
                _settings.Height = profile.DefaultHeight;
                _settings.Sampler = profile.DefaultSampler;
                _settings.Scheduler = profile.DefaultScheduler;
                _settings.Vae = profile.DefaultVae;
                _settings.TextEncoder = profile.DefaultTextEncoder;
            }

            var samplers = await _stableDiffusionService.GetSamplersAsync();
            var currentSampler = _settings.Sampler;

            if (samplers != null && samplers.Any())
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
            var currentScheduler = _settings.Scheduler;

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

            var vaes = await _stableDiffusionService.GetVaesAsync();
            var currentVae = _settings.Vae;

            if (vaes != null && vaes.Any())
            {
                if (!string.IsNullOrEmpty(currentVae) && vaes.Contains(currentVae))
                {
                    _settings.Vae = currentVae;
                }
                else if (!string.IsNullOrEmpty(profile.DefaultVae))
                {
                    _settings.Vae = vaes.FirstOrDefault(v => v.Contains(profile.DefaultVae, StringComparison.OrdinalIgnoreCase))
                        ?? vaes.FirstOrDefault();
                }
                else
                {
                    _settings.Vae = vaes.FirstOrDefault();
                }
            }

            var textEncoders = await _stableDiffusionService.GetTextEncodersAsync();
            var currentTextEncoder = _settings.TextEncoder;

            if (textEncoders != null && textEncoders.Any())
            {
                if (!string.IsNullOrEmpty(currentTextEncoder) && textEncoders.Contains(currentTextEncoder))
                {
                    _settings.TextEncoder = currentTextEncoder;
                }
                else if (!string.IsNullOrEmpty(profile.DefaultTextEncoder))
                {
                    _settings.TextEncoder = textEncoders.FirstOrDefault(v => v.Contains(profile.DefaultTextEncoder, StringComparison.OrdinalIgnoreCase))
                        ?? textEncoders.FirstOrDefault();
                }
                else
                {
                    _settings.TextEncoder = textEncoders.FirstOrDefault();
                }
            }
        }
        catch (Exception ex)
        {
            var message = "Unable to connect to the configured server. Please double check your app settings/connectivity and try again.";

            if (!string.IsNullOrEmpty(ex.Message))
            {
                message += $"\n\nMessage: {ex.Message}";
            }

            await _popupService.DisplayAlertAsync(
                "Connection problem",
                message,
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
        if (_generationTaskService.IsRunning)
        {
            await _popupService.DisplayAlertAsync(
                "Generation in progress",
                "An image generation is already running. Please wait for it to finish or cancel it.",
                "OK");
            return;
        }

#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && !_notificationPermissionRequested)
        {
            _notificationPermissionRequested = true;
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                if (status == PermissionStatus.Granted)
                {
                    Preferences.Default.Remove("NotificationPermissionDenied");
                }
                else
                {
                    var hasPreviouslyDenied = Preferences.Default.Get("NotificationPermissionDenied", false);
                    if (hasPreviouslyDenied)
                    {
                        await CommunityToolkit.Maui.Alerts.Toast.Make("Enable notifications for background progress updates").Show();
                    }
                    else
                    {
                        status = await Permissions.RequestAsync<Permissions.PostNotifications>();
                        if (status != PermissionStatus.Granted)
                        {
                            Preferences.Default.Set("NotificationPermissionDenied", true);
                            await _popupService.DisplayAlertAsync(
                                "Notification Permission Denied",
                                "Background generation notifications will not appear.",
                                "OK");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error requesting notification permission: {ex}");
            }
        }
#endif

        if (!Preferences.Default.ContainsKey(Constants.PreferenceKeys.ServerUrl))
        {
            await _popupService.DisplayAlertAsync(
                "No server URL",
                "There is no server URL configured. Please set the server URL in app settings and try again.",
                "OK");

            return;
        }

        if (!await initializeStableDiffusionService())
        {
            await ShowConnectivityStatus();
            return;
        }

        if (!await _stableDiffusionService.CheckServerAsync())
        {
            await _popupService.DisplayAlertAsync(
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

            var request = new GenerationTaskRequest
            {
                Settings = settings,
                TotalExpectedImages = settings.BatchCount * settings.BatchSize,
                SanitizedPrompt = sanitizedPrompt[..length]
            };

            if (_settings.Model != null && !string.IsNullOrEmpty(_settings.Model.Key))
            {
                var checkpointSettings = CheckpointSettings.FromPromptSettings(_settings);
                _checkpointSettingsService.Save(_settings.Model.Key, checkpointSettings);
            }

            await _generationTaskService.StartAsync(request);
        }
        catch (Exception e)
        {
            await _popupService.DisplayAlertAsync("Error", $"An unexpected error occurred: {e.Message}", "OK");
            IsGenerating = false;
            DeviceDisplay.Current.KeepScreenOn = false;
        }
    }

    private void onGenerationProgressChanged(object? sender, float progress)
    {
        reportProgress(progress);
    }

    private async void onGenerationCompleted(object? sender, GenerationTaskResult result)
    {
        try
        {
            // If we were backgrounded and the collection was cleared, rebuild placeholders
            if (Results.Count == 0 && result.Images.Count > 0)
            {
                for (var i = 0; i < result.Images.Count; i++)
                {
                    var resultItem = _serviceProvider.GetService<IResultItemViewModel>();
                    if (resultItem != null)
                    {
                        resultItem.ParentCollection = Results;
                        resultItem.ApplyQueryParamsFromResultItemCommand = new RelayCommand<IDictionary<string, object>>(ApplyQueryAttributes);
                        Results.Add(resultItem);
                    }
                }
            }

            if (result.Success)
            {
                foreach (var image in result.Images)
                {
                    var resultItem = Results.FirstOrDefault(r => r.ApiResponse == null);

                    if (resultItem != null)
                    {
                        resultItem.ApiResponse = image.Response;
                        resultItem.Settings = image.Settings;
                        resultItem.InternalUri = image.InternalUri;

                        await retrieveResultImageAsync(resultItem, image.InternalUri);
                    }
                }
            }
            else
            {
                if (result.ErrorMessage?.Contains("network error", StringComparison.OrdinalIgnoreCase) == true ||
                    result.ErrorMessage?.Contains("SocketException", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await _popupService.DisplayAlertAsync("Connection Error", $"A network error occurred: {result.ErrorMessage}", "OK");
                }
                else if (result.ErrorMessage?.Contains("web error", StringComparison.OrdinalIgnoreCase) == true ||
                         result.ErrorMessage?.Contains("WebException", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await _popupService.DisplayAlertAsync("Web Error", $"A web error occurred: {result.ErrorMessage}", "OK");
                }
                else if (result.ErrorMessage != "Generation cancelled.")
                {
                    await _popupService.DisplayAlertAsync("Error", $"An unexpected error occurred: {result.ErrorMessage}", "OK");
                }
            }

            // Any remaining results that weren't set have failed
            foreach (var resultItem in Results)
            {
                if (resultItem.IsLoading)
                {
                    resultItem.IsLoading = false;
                    resultItem.Failed = true;
                }
            }

            vibrate(HapticFeedbackType.LongPress);
        }
        finally
        {
            IsGenerating = false;
            DeviceDisplay.Current.KeepScreenOn = false;
            _generationTaskService.ClearLastResult();
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

        return Task.Run(async () =>
        {
            var stream = await _imageService.GetStreamFromContentTypeStringAsync(sourceImageString, CancellationToken.None);

            var result = _imageService.GetResizedImageStreamBytes(stream, width, height, forceExactSize, filterImage);

            if (result.Bytes == null ||
                result.Bytes.Length == 0)
            {
                return (string.Empty, 0, 0);
            }

            var imageString = Convert.ToBase64String(result.Bytes);

            return (string.Format(Constants.ImageDataFormat, "image/png", imageString), result.ActualWidth, result.ActualHeight);
        });
    }

    private async Task retrieveResultImageAsync(IResultItemViewModel result, string internalUri)
    {
        // Resize if too large
        using var fileStream = await _fileService.GetFileStreamFromInternalStorageAsync(internalUri);
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
            await _generationTaskService.CancelAsync();
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
                    await ShowConnectivityStatus();

                    return;
                }
            }
            finally
            {
                await LoadingService.HideAsync();
            }
        }

        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        await Shell.Current.GoToAsync("GenerationSettingsPage", parameters);
    }


    [RelayCommand]
    private async Task ShowConnectivityStatus()
    {
        var message = ServerConnected ? "Server connected" : "No connection to server";
        
        await _popupService.DisplayAlertAsync("Connection Status", message, "OK");
    }

    [RelayCommand]
    private Task NavigateToAboutPage()
    {
        return Shell.Current.GoToAsync("AboutPage");
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

            if (_settings.Model != null && !string.IsNullOrEmpty(_settings.Model.Key))
            {
                var checkpointSettings = CheckpointSettings.FromPromptSettings(_settings);
                _checkpointSettingsService.Save(_settings.Model.Key, checkpointSettings);
            }

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
                var resChangeResult = await _popupService.DisplayAlertAsync("Confirm Resolution Change", resChangeMessage, "CHANGE", "Keep");

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
                await _popupService.DisplayAlertAsync("Missing Resources", 
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
        var useAsSourceImage = !await _popupService.DisplayAlertAsync(
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