using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web.Mvc;
using Veerangana.YouTubeAnalytics.Infrastructure.Configuration;
using Veerangana.YouTubeAnalytics.Infrastructure.Data;
using Veerangana.YouTubeAnalytics.Models;
using Veerangana.YouTubeAnalytics.Repositories;
using Veerangana.YouTubeAnalytics.Services;
using Veerangana.YouTubeAnalytics.ViewModels;

namespace Veerangana.YouTubeAnalytics.Areas.YouTubeAnalytics.Controllers
{
    public sealed class SettingsController : AnalyticsControllerBase
    {
        private readonly IAnalyticsStoreRepository _analyticsStoreRepository;
        private readonly IYouTubeConfiguration _youTubeConfiguration;

        public SettingsController()
            : this(
                new AnalyticsStoreRepository(new SqlConnectionFactory()),
                new YouTubeConfiguration())
        {
        }

        internal SettingsController(
            IAnalyticsStoreRepository analyticsStoreRepository,
            IYouTubeConfiguration youTubeConfiguration)
        {
            if (analyticsStoreRepository == null)
            {
                throw new ArgumentNullException("analyticsStoreRepository");
            }

            if (youTubeConfiguration == null)
            {
                throw new ArgumentNullException("youTubeConfiguration");
            }

            _analyticsStoreRepository = analyticsStoreRepository;
            _youTubeConfiguration = youTubeConfiguration;
        }

        public async Task<ActionResult> Index()
        {
            DashboardShellViewModel model = BuildShell(
                "settings",
                "Data sources",
                "Monitor the SQL analytics store and YouTube integration configuration.");

            model.YouTubeConfigurationStatus = new YouTubeConfigurationStatusViewModel
            {
                IsAnalyticsReady = _youTubeConfiguration.IsAnalyticsReady,
                HasClientCredentials = _youTubeConfiguration.HasClientCredentials,
                HasRefreshToken = _youTubeConfiguration.HasRefreshToken,
                HasApiKey = !string.IsNullOrWhiteSpace(_youTubeConfiguration.ApiKey),
                HasChannelIdentity = _youTubeConfiguration.HasChannelIdentity,
                ChannelIdentity = !string.IsNullOrWhiteSpace(_youTubeConfiguration.ChannelId)
                    ? _youTubeConfiguration.ChannelId
                    : _youTubeConfiguration.ChannelHandle,
                ChannelUrl = _youTubeConfiguration.ChannelUrl
            };

            try
            {
                AnalyticsStoreStatus status = await _analyticsStoreRepository.GetStatusAsync();
                model.DatabaseStatus = new DatabaseStatusViewModel
                {
                    IsAvailable = true,
                    ServerName = status.ServerName,
                    DatabaseName = status.DatabaseName,
                    AuthenticationMode = "Windows Authentication",
                    VideoCount = status.VideoCount,
                    SnapshotCount = status.SnapshotCount,
                    LastSuccessfulSyncUtc = status.LastSuccessfulSyncUtc,
                    IsSyncRunning = status.IsSyncRunning,
                    OldestSyncedDate = status.OldestSyncedDate,
                    LatestSyncedDate = status.LatestSyncedDate,
                    LastSyncError = status.LastSyncError,
                    StatusMessage = status.IsSyncRunning ? "Synchronizing" : "Connected"
                };
                if (status.LastSuccessfulSyncUtc.HasValue)
                {
                    model.LastUpdatedUtc = status.LastSuccessfulSyncUtc.Value;
                }
            }
            catch (Exception exception)
            {
                Trace.TraceError("Analytics database status check failed: {0}", exception);
                model.DatabaseStatus = new DatabaseStatusViewModel
                {
                    IsAvailable = false,
                    ServerName = "KAJU",
                    DatabaseName = "VeeranganaYouTubeAnalytics",
                    AuthenticationMode = "Windows Authentication",
                    StatusMessage = "Unavailable"
                };
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SyncNow()
        {
            bool queued = YouTubeAnalyticsSyncScheduler.QueueNow();
            return RedirectToAction("Index", new { sync = queued ? "queued" : "disabled" });
        }
    }
}
