using System;
using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Models;
using Veerangana.YouTubeAnalytics.ViewModels;

namespace Veerangana.YouTubeAnalytics.Repositories
{
    public interface IAnalyticsFactRepository
    {
        Task<DashboardOverviewData> GetOverviewAsync(GlobalFilterViewModel filters);

        Task<AnalyticsReportData> GetReportAsync(
            AnalyticsReportKind reportKind,
            GlobalFilterViewModel filters);

        Task<AnalyticsSyncLease> TryBeginSyncAsync(string trigger, TimeSpan leaseDuration);

        Task SaveSyncBatchAsync(AnalyticsSyncBatch batch);

        Task CompleteSyncAsync(
            AnalyticsSyncLease lease,
            int recordsProcessed,
            DateTime oldestSyncedDate,
            DateTime latestSyncedDate);

        Task FailSyncAsync(AnalyticsSyncLease lease, Exception exception);
    }
}
