using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.Content;
using CommunityToolkit.Mvvm.Messaging;
using Mogri.Helpers;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Messages;
using Mogri.Models;
using Mogri.Platforms.Android.Services;
using Application = Android.App.Application;

namespace Mogri.Platforms.Android.Coordinators;

/// <summary>
/// Coordinates Android generation execution with wake locks and foreground-service handoff when the app backgrounds.
/// </summary>
public class AndroidGenerationTaskCoordinator : IGenerationTaskCoordinator
{
    private readonly IImageGenerationCoordinator _imageGenerationService;
    private readonly IFileService _fileService;
    private readonly IImageService _imageService;
    private CancellationTokenSource? _cts;
    private PowerManager.WakeLock? _wakeLock;
    private DateTime _lastNotificationUpdate = DateTime.MinValue;
    private bool _isAppInForeground = true;
    private float _currentProgress;

    public bool IsRunning { get; private set; }
    public GenerationTaskResult? LastResult { get; private set; }

    public event EventHandler<float>? ProgressChanged;
    public event EventHandler<GenerationTaskResult>? Completed;

    public AndroidGenerationTaskCoordinator(
        IImageGenerationCoordinator imageGenerationService,
        IFileService fileService,
        IImageService imageService)
    {
        _imageGenerationService = imageGenerationService;
        _fileService = fileService;
        _imageService = imageService;

        WeakReferenceMessenger.Default.Register<AppLifecycleMessage>(this, (r, m) =>
        {
            _isAppInForeground = m.Value;
            if (IsRunning)
            {
                if (_isAppInForeground)
                {
                    AndroidGenerationForegroundService.Instance?.StopForegroundService(true);
                }
                else
                {
                    StartForegroundServiceIfRunning();
                }
            }
        });
    }

    private async void StartForegroundServiceIfRunning()
    {
        if (!IsRunning)
        {
            return;
        }

        var hasNotificationPermission = true;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            hasNotificationPermission = ContextCompat.CheckSelfPermission(Application.Context, "android.permission.POST_NOTIFICATIONS") == Permission.Granted;
        }

        if (hasNotificationPermission)
        {
            var intent = new Intent(Application.Context, typeof(AndroidGenerationForegroundService));
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                Application.Context.StartForegroundService(intent);
            }
            else
            {
                Application.Context.StartService(intent);
            }

            var retries = 0;
            while (AndroidGenerationForegroundService.Instance == null && retries < 10)
            {
                await Task.Delay(100);
                retries++;
            }

            if (AndroidGenerationForegroundService.Instance != null)
            {
                if (_isAppInForeground || !IsRunning)
                {
                    AndroidGenerationForegroundService.Instance.StopForegroundService(true);
                }
                else
                {
                    AndroidGenerationForegroundService.Instance.UpdateProgress(_currentProgress);
                }
            }
        }
    }

    public Task StartAsync(GenerationTaskRequest request)
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        IsRunning = true;
        _currentProgress = 0f;
        _cts = new CancellationTokenSource();
        var syncContext = SynchronizationContext.Current;

        var powerManager = (PowerManager?)Application.Context.GetSystemService(Context.PowerService);
        _wakeLock = powerManager?.NewWakeLock(WakeLockFlags.Partial, "Mogri::GenerationWakeLock");
        _wakeLock?.Acquire();

        if (!_isAppInForeground)
        {
            StartForegroundServiceIfRunning();
        }

        _ = Task.Run(async () => await RunGenerationLoopAsync(request, syncContext));

        return Task.CompletedTask;
    }

    private async Task RunGenerationLoopAsync(GenerationTaskRequest request, SynchronizationContext? syncContext)
    {
        var result = new GenerationTaskResult { Success = true };
        var imageNumber = 1;

        try
        {
            if (_cts == null)
            {
                return;
            }

            await foreach (var response in _imageGenerationService.SubmitImageRequestAsync(request.Settings, _cts.Token))
            {
                if (response.ResponseObject is ProgressResponse progressResponse)
                {
                    _currentProgress = (float)progressResponse.Progress;
                    RaiseProgressChanged(syncContext, _currentProgress);
                    UpdateNotificationProgress(_currentProgress);
                }
                else if (response.ResponseObject is GenerationResponse generationResponse)
                {
                    if (generationResponse.Images != null)
                    {
                        foreach (var imageBase64 in generationResponse.Images)
                        {
                            var imageBytes = Convert.FromBase64String(imageBase64);
                            var imageSettings = request.Settings.Clone();
                            imageSettings.Seed = generationResponse.Seeds?.ElementAtOrDefault(imageNumber - 1) ?? request.Settings.Seed + (imageNumber - 1);

                            imageBytes = PngMetadataHelper.WriteSettings(imageBytes, imageSettings);

                            var fileName = $"{request.SanitizedPrompt}-{imageSettings.Seed}-{DateTime.Now.Ticks}-{imageNumber}.png";
                            var internalUri = await _fileService.WriteFileToInternalStorageAsync(fileName, imageBytes);

                            result.Images.Add(new GenerationResultImage
                            {
                                InternalUri = internalUri,
                                Settings = imageSettings,
                                Response = response,
                                ImageNumber = imageNumber
                            });

                            imageNumber++;
                        }
                    }
                }
                else if (response.Progress > 0)
                {
                    _currentProgress = (float)response.Progress;
                    RaiseProgressChanged(syncContext, _currentProgress);
                    UpdateNotificationProgress(_currentProgress);
                }
            }

            var service = AndroidGenerationForegroundService.Instance;
            if (service != null)
            {
                service.ShowCompleted(result.Images.Count);
            }
        }
        catch (System.OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Generation cancelled.";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;

            var service = AndroidGenerationForegroundService.Instance;
            if (service != null)
            {
                service.ShowFailed(ex.Message);
            }
        }
        finally
        {
            IsRunning = false;
            LastResult = result;

            if (_wakeLock?.IsHeld == true)
            {
                _wakeLock.Release();
            }

            var removeNotification = result.ErrorMessage == "Generation cancelled.";
            AndroidGenerationForegroundService.Instance?.StopForegroundService(removeNotification);

            _cts?.Dispose();
            _cts = null;

            RaiseCompleted(syncContext, result);
        }
    }

    public void ClearLastResult()
    {
        LastResult = null;
    }

    private void UpdateNotificationProgress(float progress)
    {
        var service = AndroidGenerationForegroundService.Instance;
        if (service == null)
        {
            return;
        }

        var now = DateTime.Now;
        if ((now - _lastNotificationUpdate).TotalMilliseconds > 500 || progress >= 1.0f)
        {
            service.UpdateProgress(progress);
            _lastNotificationUpdate = now;
        }
    }

    public Task CancelAsync()
    {
        _cts?.Cancel();
        _imageGenerationService.CancelAsync();

        return Task.CompletedTask;
    }

    private void RaiseProgressChanged(SynchronizationContext? syncContext, float progress)
    {
        if (syncContext != null)
        {
            syncContext.Post(_ => ProgressChanged?.Invoke(this, progress), null);
        }
        else
        {
            ProgressChanged?.Invoke(this, progress);
        }
    }

    private void RaiseCompleted(SynchronizationContext? syncContext, GenerationTaskResult result)
    {
        if (syncContext != null)
        {
            syncContext.Post(_ => Completed?.Invoke(this, result), null);
        }
        else
        {
            Completed?.Invoke(this, result);
        }
    }
}