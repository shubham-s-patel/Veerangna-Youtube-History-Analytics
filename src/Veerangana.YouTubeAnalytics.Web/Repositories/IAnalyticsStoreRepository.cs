using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Repositories
{
    public interface IAnalyticsStoreRepository
    {
        Task<AnalyticsStoreStatus> GetStatusAsync();
    }
}
