using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Models;
using Veerangana.YouTubeAnalytics.ViewModels;

namespace Veerangana.YouTubeAnalytics.Services
{
    public interface IAnalyticsWorkspaceService
    {
        Task<AnalyticsReportData> GetReportAsync(
            AnalyticsReportKind reportKind,
            GlobalFilterViewModel filters);
    }
}
