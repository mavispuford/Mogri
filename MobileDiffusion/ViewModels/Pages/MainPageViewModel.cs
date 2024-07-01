using MobileDiffusion.Interfaces.ViewModels;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Models;
using System.Collections.ObjectModel;
using SkiaSharp.Views.Maui.Controls;
using CommunityToolkit.Maui.Alerts;
using MobileDiffusion.Models.LStein;
using MobileDiffusion.Clients.Automatic1111;
using Newtonsoft.Json;
using MobileDiffusion.Json;

namespace MobileDiffusion.ViewModels;

public partial class MainPageViewModel : PageViewModel, IMainPageViewModel
{
    const string _defaultPrompt = "An astronaut floating in space, detailed digital drawing, octane render, trending on artstation";

    private readonly IFileService _fileService;
    private readonly IStableDiffusionService _stableDiffusionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IImageService _imageService;

    private PromptSettings _settings = new();
    private string _resizedInitImage;
    private string _resizedMaskImage;
    private bool _initImageNeedsResize = true;
    private float _targetProgress = 0;

    [ObservableProperty]
    private bool _hasInitImage;

    [ObservableProperty]
    private string _prompt = _defaultPrompt;

    [ObservableProperty]
    private string _negativePrompt;

    [ObservableProperty]
    private float _progress;

    [ObservableProperty]
    private bool _serverConnected;

    [ObservableProperty]
    private ObservableCollection<IResultItemViewModel> _results = new();

    public MainPageViewModel(
        IFileService fileService,
        IStableDiffusionService stableDiffusionService,
        IServiceProvider serviceProvider,
        IImageService imageService)
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
        if (_stableDiffusionService.Initialized)
        {
            ServerConnected = true;
            return true;
        }

        try
        {
            await _stableDiffusionService.InitializeAsync();

            var samplers = await _stableDiffusionService.GetSamplersAsync();

            _settings.Sampler = samplers?.FirstOrDefault().Key ?? "Euler";

            _settings.Model = (ModelViewModel)await _stableDiffusionService.GetSelectedModelAsync();
        }
        catch
        {
            await Shell.Current.CurrentPage.DisplayAlert(
                "Connection problems",
                "Unable to connect to the configured server URL. Please double check your app settings/connectivity and try again.",
                "OK");

            ServerConnected = false;
            return false;
        }

        ServerConnected = true;
        return true;
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

        if (!await initializeStableDiffusionService())
        {
            return;
        }

        if (!await _stableDiffusionService.CheckServerAsync())
        {
            await Shell.Current.CurrentPage.DisplayAlert(
                "Connection Problems",
                "Unable to connect to the server. Please verify your connectivity and try again.",
                "OK");

            return;
        }

        Progress = 0;

        Results = new();

        var settings = _settings.Clone();

        for (var i = 0; i < settings.BatchCount * settings.BatchSize; i++)
        {
            var resultItem = _serviceProvider.GetService<IResultItemViewModel>();

            resultItem.ApplyQueryParamsFromResultItemCommand = new RelayCommand<IDictionary<string,object>>(ApplyQueryAttributes);

            Results.Add(resultItem);
        }

        if (HasInitImage && settings.FitClientSide)
        {
            if (_initImageNeedsResize)
            {
                var initImageResult = await GetResizedImageStringFromSettingsAsync(settings, settings.InitImage, filterImage: true);

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
                if (response.StableDiffusionApi == Enums.StableDiffusionApi.InvokeAI)
                {
                    var item = response.ResponseObject as LSteinResponseItem;

                    if (item.Event.Equals("step", StringComparison.OrdinalIgnoreCase))
                    {
                        float progress = 0f;

                        if (string.IsNullOrEmpty(settings.InitImage))
                        {
                            progress = item.Step / (float)(settings.Steps);
                        }
                        else
                        {
                            progress = item.Step / (float)(settings.Steps * settings.DenoisingStrength);
                        }

                        reportProgress(progress);

                        continue;
                    }

                    var fileNameNoExtension = $"{sanitizedPrompt[..length]}-{item.Seed}-{DateTime.Now.Ticks}";

                    if (item.Event.Equals("result", StringComparison.OrdinalIgnoreCase))
                    {
                        var result = Results.FirstOrDefault(r => r.ApiResponse == null);

                        result.ApiResponse = response;
                        result.Settings = settings.Clone();

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
                        foreach (var result in Results)
                        {
                            if (result != null)
                            {
                                await retrieveResultImageAsync(result, fileNameNoExtension, imageNumber++);
                            }
                        }
                    }
                }
                else if (response.StableDiffusionApi == Enums.StableDiffusionApi.Automatic1111)
                {
                    reportProgress((float)response.Progress);

                    if (response.ResponseObject is TextToImageResponse textToImageResponse)
                    {
                        var autoResponseInfo = JsonConvert.DeserializeObject<IDictionary<string, object>>(textToImageResponse.Info, new JsonSerializerSettings
                        {
                            ContractResolver = CustomContractResolver.Instance
                        });

                        foreach (var image in textToImageResponse.Images)
                        {
                            var seeds = autoResponseInfo["all_seeds"] as List<long>;
                            var seedString = seeds?.ElementAt(imageNumber) ?? settings.Seed + imageNumber;

                            var fileNameNoExtension = $"{sanitizedPrompt[..length]}-{seedString}-{DateTime.Now.Ticks}";

                            var result = Results.FirstOrDefault(r => r.ApiResponse == null);

                            if (result != null)
                            {
                                result.ApiResponse = response;
                                result.Settings = settings.Clone();
                                result.Settings.Seed = seedString;

                                await retrieveResultImageAsync(result, fileNameNoExtension, imageNumber++);
                            }
                        }
                    }
                    else if (response.ResponseObject is ImageToImageResponse imageToImageResponse)
                    {
                        reportProgress((float)response.Progress);

                        var autoResponseInfo = JsonConvert.DeserializeObject<IDictionary<string, object>>(imageToImageResponse.Info, new JsonSerializerSettings
                        {
                            ContractResolver = CustomContractResolver.Instance
                        });

                        foreach (var image in imageToImageResponse.Images)
                        {
                            var seeds = autoResponseInfo["all_seeds"] as List<long>;
                            var seedString = seeds?.ElementAt(imageNumber) ?? settings.Seed + imageNumber;

                            var fileNameNoExtension = $"{sanitizedPrompt[..length]}-{seedString}-{DateTime.Now.Ticks}";

                            var result = Results.FirstOrDefault(r => r.ApiResponse == null);

                            result.ApiResponse = response;
                            result.Settings = settings.Clone();
                            result.Settings.Seed = seedString;

                            await retrieveResultImageAsync(result, fileNameNoExtension, imageNumber++);
                        }
                    }
                    else if (response.ResponseObject is Modules__api__models__ProgressResponse progressResponse)
                    {
                        // TODO - Display "current image"
                    }              
                }
            }
        }
        catch (System.Net.Sockets.SocketException socketException)
        {
            // TODO - Handle timeouts

            //return;
        }
        catch (System.Net.WebException webException)
        {
            // TODO - Handle this

            //return;
        }
        catch (Exception e)
        {
            // TODO - Handle this

            //return;
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
        byte[] imageBytes = null;

        if (result.ApiResponse.StableDiffusionApi == Enums.StableDiffusionApi.InvokeAI)
        {
            var invokeAiResponse = result.ApiResponse.ResponseObject as LSteinResponseItem;

            imageBytes = await _stableDiffusionService.GetImageBytesAsync(invokeAiResponse.Url);
        }
        else if (result.ApiResponse.StableDiffusionApi == Enums.StableDiffusionApi.Automatic1111)
        {
            if (result.ApiResponse.ResponseObject is TextToImageResponse textToImageResponse)
            {
                imageBytes = Convert.FromBase64String(textToImageResponse.Images.ElementAt(number));
            }
            else if (result.ApiResponse.ResponseObject is ImageToImageResponse imageToImageResponse)
            {
                imageBytes = Convert.FromBase64String(imageToImageResponse.Images.ElementAt(number));
            }
        }

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
            // TODO - Show that app is busy here

            if (!await initializeStableDiffusionService())
            {
                return;
            }
        }

        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        await Shell.Current.GoToAsync("PromptSettingsPage", parameters);
    }

    public override async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(NavigationParams.PromptSettings, out var promptSettings) &&
            promptSettings is PromptSettings settings)
        {
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

            updateHasInitImage();
        }

        if (query.TryGetValue(NavigationParams.MaskImgString, out var maskImageParam))
        {
            _settings.Mask = maskImageParam as string;

            updateHasInitImage();
        }

        if (query.TryGetValue(NavigationParams.CanvasImageString, out var canvasImageParam))
        {
            var parameters = new Dictionary<string, object>
            {
                {NavigationParams.CanvasImageString, canvasImageParam as string}
            };

            await Shell.Current.Dispatcher.DispatchAsync(async () =>
            {
                await Shell.Current.Navigation.PopToRootAsync();
                await Shell.Current.GoToAsync("///CanvasPageTab", parameters);
            });
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
                var resChangeResult = await Shell.Current.DisplayAlert("Confirm Resolution Change", resChangeMessage, "CHANGE", "Keep");

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

            await LoadSharedImage(imageUri, appShareContentTypeParam as string);
        }

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    public override Task OnAppearingAsync()
    {
        return base.OnAppearingAsync();
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