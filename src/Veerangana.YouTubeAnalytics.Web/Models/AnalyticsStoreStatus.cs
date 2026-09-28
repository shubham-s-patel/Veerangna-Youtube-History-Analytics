using System;

namespace Veerangana.YouTubeAnalytics.Models
{
    public sealed class AnalyticsStoreStatus
    {
        public string ServerName { get; set; }

        public string DatabaseName { get; set; }

        public long VideoCount { get; set; }

        public long SnapshotCount { get; set; }

        public DateTime? LastSuccessfulSyncUtc { get; set; }

        public bool IsSyncRunning { get; set; }

        public DateTime? OldestSyncedDate { get; set; }

        public DateTime? LatestSyncedDate { get; set; }

        public string LastSyncError { get; set; }
    }
}
