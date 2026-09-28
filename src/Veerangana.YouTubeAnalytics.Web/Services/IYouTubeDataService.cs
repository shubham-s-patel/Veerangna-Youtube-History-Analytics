using System.Collections.Generic;
using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Services
{
    public interface IYouTubeDataService
    {
        Task<YouTubeChannelInfo> GetAuthorizedChannelAsync();

        Task<IList<YouTubeVideoInfo>> GetVideosAsync(IEnumerable<string> videoIds);
    }
}
