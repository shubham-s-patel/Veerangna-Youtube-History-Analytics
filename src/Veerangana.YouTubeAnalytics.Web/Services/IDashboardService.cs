using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Models;
using Veerangana.YouTubeAnalytics.ViewModels;

namespace Veerangana.YouTubeAnalytics.Services
{
    public interface IDashboardService
    {
        Task<DashboardOverviewData> GetOverviewAsync(GlobalFilterViewModel filters);
    }
}
