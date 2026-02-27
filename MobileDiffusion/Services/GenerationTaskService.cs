using System;
using System.Threading;
using System.Threading.Tasks;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Models;
using MobileDiffusion.Helpers;

namespace MobileDiffusion.Services
{
    /// <summary>
    /// Standard implementation of the generation task service for non-Android platforms.
    /// Executes the generation loop asynchronously but does not currently implement
    /// platform-specific background keep-alive mechanisms.
    /// </summary>
    public class GenerationTaskService : IGenerationTaskService
    {
        private readonly IImageGenerationService _imageGenerationService;
        private readonly IFileService _fileService;
        private readonly IImageService _imageService;
        private CancellationTokenSource? _cts;

        public bool IsRunning { get; private set; }
        public GenerationTaskResult? LastResult { get; private set; }

        public event EventHandler<float>? ProgressChanged;
        public event EventHandler<GenerationTaskResult>? Completed;

        public GenerationTaskService(
            IImageGenerationService imageGenerationService,
            IFileService fileService,
            IImageService imageService)
        {
            _imageGenerationService = imageGenerationService;
            _fileService = fileService;
            _imageService = imageService;
        }

        public async Task StartAsync(GenerationTaskRequest request)
        {
            if (IsRunning)
            {
                return;
            }

            IsRunning = true;
            _cts = new CancellationTokenSource();
            var syncContext = SynchronizationContext.Current;

            var result = new GenerationTaskResult { Success = true };
            var imageNumber = 1;

            try
            {
                await foreach (var response in _imageGenerationService.SubmitImageRequestAsync(request.Settings, _cts.Token))
                {
                    if (response.ResponseObject is ProgressResponse progressResponse)
                    {
                        RaiseProgressChanged(syncContext, (float)progressResponse.Progress);
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
                        RaiseProgressChanged(syncContext, (float)response.Progress);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "Generation cancelled.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                IsRunning = false;
                LastResult = result;
                _cts?.Dispose();
                _cts = null;
                RaiseCompleted(syncContext, result);
            }
        }

        public void ClearLastResult()
        {
            LastResult = null;
        }

        public Task CancelAsync()
        {
            _cts?.Cancel();
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
