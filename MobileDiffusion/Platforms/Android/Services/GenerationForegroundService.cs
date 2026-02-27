using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace MobileDiffusion.Platforms.Android.Services
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

        private const string ChannelId = "image_generation";
        private const int NotificationId = 1001;
        private NotificationManager? _notificationManager;
        private NotificationCompat.Builder? _notificationBuilder;

        public override void OnCreate()
        {
            base.OnCreate();
            Instance = this;

            _notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            CreateNotificationChannel();
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            var pendingIntent = CreatePendingIntent();

            _notificationBuilder = new NotificationCompat.Builder(this, ChannelId);
            _notificationBuilder.SetSmallIcon(Resource.Mipmap.appicon);
            _notificationBuilder.SetContentTitle("Generating Image…");
            _notificationBuilder.SetContentText("0% complete");
            _notificationBuilder.SetProgress(100, 0, true);
            _notificationBuilder.SetContentIntent(pendingIntent);
            _notificationBuilder.SetOngoing(true);
            _notificationBuilder.SetPriority(NotificationCompat.PriorityLow);
            _notificationBuilder.SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate);

            if (_notificationBuilder != null)
            {
                StartForeground(NotificationId, _notificationBuilder.Build());
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
            if (_notificationBuilder == null || _notificationManager == null) return;

            var progressPercent = (int)(progress * 100);
            
            _notificationBuilder.SetContentText($"{progressPercent}% complete");
            _notificationBuilder.SetProgress(100, progressPercent, false);

            _notificationManager.Notify(NotificationId, _notificationBuilder.Build());
        }

        public void ShowCompleted(int imageCount)
        {
            if (_notificationBuilder == null || _notificationManager == null) return;

            _notificationBuilder.SetContentTitle("Generation Complete");
            _notificationBuilder.SetContentText($"{imageCount} image{(imageCount == 1 ? "" : "s")} generated");
            _notificationBuilder.SetProgress(0, 0, false);
            _notificationBuilder.SetOngoing(false);
            _notificationBuilder.SetAutoCancel(true);
            _notificationBuilder.SetTimeoutAfter(5000); // Auto-dismiss after 5 seconds

            _notificationManager.Notify(NotificationId, _notificationBuilder.Build());
        }

        public void ShowFailed(string message)
        {
            if (_notificationBuilder == null || _notificationManager == null) return;

            _notificationBuilder.SetContentTitle("Generation Failed");
            _notificationBuilder.SetContentText($"Error: {message}");
            _notificationBuilder.SetProgress(0, 0, false);
            _notificationBuilder.SetOngoing(false);
            _notificationBuilder.SetAutoCancel(true);
            _notificationBuilder.SetTimeoutAfter(10000); // Auto-dismiss after 10 seconds

            _notificationManager.Notify(NotificationId, _notificationBuilder.Build());
        }

        public void StopForegroundService(bool removeNotification = true)
        {
            ServiceCompat.StopForeground(this, removeNotification ? ServiceCompat.StopForegroundRemove : ServiceCompat.StopForegroundDetach);
            
            StopSelf();
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(ChannelId, "Image Generation", NotificationImportance.Low)
                {
                    Description = "Shows progress for background image generation"
                };

                _notificationManager?.CreateNotificationChannel(channel);
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
