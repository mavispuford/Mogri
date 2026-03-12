using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace Mogri.Platforms.Android.Services
{
    /// <summary>
    /// An Android Foreground Service that displays a persistent notification
    /// while image generation is in progress. This prevents the Android OS
    /// from killing the app process when it is sent to the background.
    /// </summary>
    [Service(ForegroundServiceType = ForegroundService.TypeDataSync)]
    public class GenerationForegroundService : Service
    {
        public static GenerationForegroundService? Instance { get; private set; }

        private const string ProgressChannelId = "image_generation_progress";
        private const string CompletionChannelId = "image_generation_completion";
        private const int ProgressNotificationId = 1001;
        private const int CompletionNotificationId = 1002;
        private NotificationManager? _notificationManager;
        private NotificationCompat.Builder? _progressNotificationBuilder;
        private NotificationCompat.Builder? _completionNotificationBuilder;

        public override void OnCreate()
        {
            base.OnCreate();
            Instance = this;

            _notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            CreateNotificationChannel();
            
            var pendingIntent = CreatePendingIntent();

            // Builder for the ongoing progress notification
            _progressNotificationBuilder = new NotificationCompat.Builder(this, ProgressChannelId)
                .SetSmallIcon(Mogri.Resource.Mipmap.appicon)
                .SetContentTitle("Generating Image…")
                .SetContentText("0% complete")
                .SetProgress(100, 0, true)
                .SetContentIntent(pendingIntent)
                .SetOngoing(true)
                .SetPriority(NotificationCompat.PriorityLow) // Low priority helps prevent sound/vibration for progress
                .SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate);

            // Builder for the completion/failure notification
            _completionNotificationBuilder = new NotificationCompat.Builder(this, CompletionChannelId)
                .SetSmallIcon(Mogri.Resource.Mipmap.appicon)
                .SetContentIntent(pendingIntent)
                .SetOngoing(false)
                .SetAutoCancel(true)
                .SetPriority(NotificationCompat.PriorityHigh); // High priority allows for sound/vibration
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            if (_progressNotificationBuilder != null)
            {
                StartForeground(ProgressNotificationId, _progressNotificationBuilder.Build());
            }

            return StartCommandResult.NotSticky;
        }

        public override void OnTaskRemoved(Intent? rootIntent)
        {
            base.OnTaskRemoved(rootIntent);
            
            // The actual cancellation will be handled by the AndroidGenerationTaskService
            // when it detects the service is stopping, or we could expose an event here.
            // For now, just stop the service.
            StopForegroundService();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            Instance = null;
        }

        public override IBinder? OnBind(Intent? intent)
        {
            return null;
        }

        public void UpdateProgress(float progress)
        {
            if (_progressNotificationBuilder == null || _notificationManager == null) return;

            var progressPercent = (int)(progress * 100);
            
            _progressNotificationBuilder.SetContentText($"{progressPercent}% complete");
            _progressNotificationBuilder.SetProgress(100, progressPercent, false);

            _notificationManager.Notify(ProgressNotificationId, _progressNotificationBuilder.Build());
        }

        public void ShowCompleted(int imageCount)
        {
            if (_completionNotificationBuilder == null || _notificationManager == null) return;

            _completionNotificationBuilder.SetContentTitle("Generation Complete");
            _completionNotificationBuilder.SetContentText($"{imageCount} image{(imageCount == 1 ? "" : "s")} generated");
            _completionNotificationBuilder.SetTimeoutAfter(5000); // Auto-dismiss after 5 seconds
        }

        public void ShowFailed(string message)
        {
            if (_completionNotificationBuilder == null || _notificationManager == null) return;

            _completionNotificationBuilder.SetContentTitle("Generation Failed");
            _completionNotificationBuilder.SetContentText($"Error: {message}");
            _completionNotificationBuilder.SetTimeoutAfter(10000); // Auto-dismiss after 10 seconds
        }

        public void StopForegroundService(bool removeNotification = true)
        {
            // Always remove the foreground state with its tied notification to prevent Android reverting it
            ServiceCompat.StopForeground(this, ServiceCompat.StopForegroundRemove);
            
            // Re-post the completion notification if requested
            if (!removeNotification && _completionNotificationBuilder != null && _notificationManager != null)
            {
                _notificationManager.Notify(CompletionNotificationId, _completionNotificationBuilder.Build());
            }

            StopSelf();
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var progressChannel = new NotificationChannel(ProgressChannelId, "Image Generation Progress", NotificationImportance.Low)
                {
                    Description = "Shows silent progress for background image generation"
                };
                
                var completionChannel = new NotificationChannel(CompletionChannelId, "Image Generation Alerts", NotificationImportance.High)
                {
                    Description = "Alerts you when an image generation finishes or fails"
                };

                _notificationManager?.CreateNotificationChannel(progressChannel);
                _notificationManager?.CreateNotificationChannel(completionChannel);
            }
        }

        private PendingIntent? CreatePendingIntent()
        {
            var intent = new Intent(this, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            var flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
            
            return PendingIntent.GetActivity(this, 0, intent, flags);
        }
    }
}
