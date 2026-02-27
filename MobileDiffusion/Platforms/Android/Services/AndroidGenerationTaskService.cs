using System;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.OS;
using Android.Content.PM;
using AndroidX.Core.Content;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Models;
using MobileDiffusion.Helpers;
using Application = Android.App.Application;
using CommunityToolkit.Mvvm.Messaging;
using MobileDiffusion.Messages;

namespace MobileDiffusion.Platforms.Android.Services
{
    /// <summary>
    /// Android-specific implementation of the generation task service.
    /// Manages WakeLocks and a Foreground Service to ensure image generation
    /// continues even when the application is sent to the background.
    /// </summary>
    public class AndroidGenerationTaskService : IGenerationTaskService
    {
        private readonly IImageGenerationService _imageGenerationService;
        private readonly IFileService _fileService;
        private readonly IImageService _imageService;
        private CancellationTokenSource? _cts;
        private PowerManager.WakeLock? _wakeLock;
        private DateTime _lastNotificationUpdate = DateTime.MinValue;
        private bool _isAppInForeground = true;
        private float _currentProgress = 0f;

        public bool IsRunning { get; private set; }
        public GenerationTaskResult? LastResult { get; private set; }

        public event EventHandler<float>? ProgressChanged;
        public event EventHandler<GenerationTaskResult>? Completed;

        public AndroidGenerationTaskService(
            IImageGenerationService imageGenerationService,
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
                        GenerationForegroundService.Instance?.StopForegroundService(true);
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
            if (!IsRunning) return;

            var hasNotificationPermission = true;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
                hasNotificationPermission = ContextCompat.CheckSelfPermission(Application.Context, "android.permission.POST_NOTIFICATIONS") == Permission.Granted;
            }

            if (hasNotificationPermission)
            {
                var intent = new Intent(Application.Context, typeof(GenerationForegroundService));
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    Application.Context.StartForegroundService(intent);
                }
                else
                {
                    Application.Context.StartService(intent);
                }

                // The Foreground Service takes a moment to start and set its Instance property.
                // We poll briefly to ensure we can update its state immediately if needed.
                var retries = 0;
                while (GenerationForegroundService.Instance == null && retries < 10)
                {
                    await Task.Delay(100);
                    retries++;
                }

                if (GenerationForegroundService.Instance != null)
                {
                    if (_isAppInForeground || !IsRunning)
                    {
                        GenerationForegroundService.Instance.StopForegroundService(true);
                    }
                    else
                    {
                        GenerationForegroundService.Instance.UpdateProgress(_currentProgress);
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

            // Acquire WakeLock
            var powerManager = (PowerManager?)Application.Context.GetSystemService(Context.PowerService);
            _wakeLock = powerManager?.NewWakeLock(WakeLockFlags.Partial, "MobileDiffusion::GenerationWakeLock");
            _wakeLock?.Acquire();

            if (!_isAppInForeground)
            {
                StartForegroundServiceIfRunning();
            }

            // Run generation on a background thread so we return to the caller immediately
            _ = Task.Run(async () => await RunGenerationLoopAsync(request, syncContext));
            
            return Task.CompletedTask;
        }

        private async Task RunGenerationLoopAsync(GenerationTaskRequest request, SynchronizationContext? syncContext)
        {
            var result = new GenerationTaskResult { Success = true };
            var imageNumber = 1;

            try
            {
                if (_cts == null) return;

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
                                
                                // Clone settings for this specific image
                                var imageSettings = request.Settings.Clone();
                                imageSettings.Seed = generationResponse.Seeds?.ElementAtOrDefault(imageNumber - 1) ?? request.Settings.Seed + (imageNumber - 1);

                                // Write metadata
                                imageBytes = PngMetadataHelper.WriteSettings(imageBytes, imageSettings);

                                // Generate filename
                                var fileName = $"{request.SanitizedPrompt}-{imageSettings.Seed}-{DateTime.Now.Ticks}-{imageNumber}.png";

                                // Write to storage
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

                var service = GenerationForegroundService.Instance;
                if (service != null)
                {
                    service.ShowCompleted(result.Images.Count);
                    await Task.Delay(100);
                }
            }
            catch (System.OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "Generation cancelled.";
                // Don't show failed notification for cancellation
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                
                var service = GenerationForegroundService.Instance;
                if (service != null)
                {
                    service.ShowFailed(ex.Message);
                    await Task.Delay(100);
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
                
                // If cancelled, remove notification. If completed/failed, keep it (it will auto-dismiss).
                var removeNotification = result.ErrorMessage == "Generation cancelled.";
                GenerationForegroundService.Instance?.StopForegroundService(removeNotification);
                
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
            var service = GenerationForegroundService.Instance;
            if (service == null) return;

            // Rate limit notification updates to ~500ms
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
            
            // The service will be stopped in the finally block of RunGenerationLoopAsync
            // when the OperationCanceledException is caught.
            
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
}
