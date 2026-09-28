using System;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using System.Web.Hosting;
using Veerangana.YouTubeAnalytics.Infrastructure.Configuration;
using Veerangana.YouTubeAnalytics.Infrastructure.Data;
using Veerangana.YouTubeAnalytics.Repositories;

namespace Veerangana.YouTubeAnalytics.Services
{
    public sealed class YouTubeAnalyticsSyncScheduler : IRegisteredObject
    {
        private static readonly object InstanceLock = new object();
        private static YouTubeAnalyticsSyncScheduler _instance;

        private readonly Timer _timer;
        private int _isExecuting;
        private int _isStopping;

        private YouTubeAnalyticsSyncScheduler(TimeSpan startupDelay, TimeSpan interval)
        {
            Interval = interval;
            _timer = new Timer(Execute, null, startupDelay, interval);
        }

        public TimeSpan Interval { get; private set; }

        public static void Start()
        {
            bool enabled;
            if (!bool.TryParse(ConfigurationManager.AppSettings["YouTubeSyncEnabled"], out enabled) || !enabled)
            {
                return;
            }

            lock (InstanceLock)
            {
                if (_instance != null) return;

                int startupSeconds = ReadInt("YouTubeSyncStartupDelaySeconds", 15, 1, 600);
                int intervalMinutes = ReadInt("YouTubeSyncIntervalMinutes", 360, 15, 1440);
                _instance = new YouTubeAnalyticsSyncScheduler(
                    TimeSpan.FromSeconds(startupSeconds),
                    TimeSpan.FromMinutes(intervalMinutes));
                HostingEnvironment.RegisterObject(_instance);
            }
        }

        public static bool QueueNow()
        {
            lock (InstanceLock)
            {
                if (_instance == null || Volatile.Read(ref _instance._isStopping) == 1) return false;
                _instance._timer.Change(TimeSpan.Zero, _instance.Interval);
                return true;
            }
        }

        public void Stop(bool immediate)
        {
            if (Interlocked.Exchange(ref _isStopping, 1) == 1) return;
            _timer.Dispose();
            HostingEnvironment.UnregisterObject(this);
            lock (InstanceLock)
            {
                if (ReferenceEquals(_instance, this)) _instance = null;
            }
        }

        private async void Execute(object state)
        {
            if (Volatile.Read(ref _isStopping) == 1 ||
                Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
            {
                return;
            }

            try
            {
                await CreateSyncService().RunAsync("Scheduled").ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Trace.TraceError("Scheduled YouTube synchronization failed: {0}", exception);
            }
            finally
            {
                Interlocked.Exchange(ref _isExecuting, 0);
            }
        }

        private static IAnalyticsSyncService CreateSyncService()
        {
            IYouTubeConfiguration configuration = new YouTubeConfiguration();
            IYouTubeCredentialProvider credentials = new YouTubeCredentialProvider(configuration);
            IDbConnectionFactory connectionFactory = new SqlConnectionFactory();
            return new AnalyticsSyncService(
                new YouTubeDataService(credentials, configuration),
                new YouTubeAnalyticsService(credentials, configuration),
                new AnalyticsFactRepository(connectionFactory));
        }

        private static int ReadInt(string key, int defaultValue, int minimum, int maximum)
        {
            int value;
            return int.TryParse(ConfigurationManager.AppSettings[key], out value)
                ? Math.Max(minimum, Math.Min(maximum, value))
                : defaultValue;
        }
    }
}
