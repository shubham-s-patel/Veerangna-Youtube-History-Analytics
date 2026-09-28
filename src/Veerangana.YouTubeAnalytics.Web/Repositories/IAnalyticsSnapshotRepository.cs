using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Repositories
{
    public interface IAnalyticsSnapshotRepository
    {
        Task SaveOverviewSnapshotAsync(
            YouTubeChannelInfo channel,
            IEnumerable<YouTubeVideoInfo> videos,
            DateTime snapshotDate);
    }
}
