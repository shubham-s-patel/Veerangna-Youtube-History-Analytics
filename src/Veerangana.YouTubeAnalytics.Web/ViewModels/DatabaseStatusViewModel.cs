using System;

namespace Veerangana.YouTubeAnalytics.ViewModels
{
    public sealed class DatabaseStatusViewModel
    {
        public bool IsAvailable { get; set; }

        public string ServerName { get; set; }

        public string DatabaseName { get; set; }

        public string AuthenticationMode { get; set; }

        public long VideoCount { get; set; }

        public long SnapshotCount { get; set; }

        public DateTime? LastSuccessfulSyncUtc { get; set; }

        public bool IsSyncRunning { get; set; }

        public DateTime? OldestSyncedDate { get; set; }

        public DateTime? LatestSyncedDate { get; set; }

        public string LastSyncError { get; set; }

        public string StatusMessage { get; set; }
    }
}
