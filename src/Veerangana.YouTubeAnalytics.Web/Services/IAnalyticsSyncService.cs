using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Services
{
    public interface IAnalyticsSyncService
    {
        Task<AnalyticsSyncResult> RunAsync(string trigger);
    }
}
