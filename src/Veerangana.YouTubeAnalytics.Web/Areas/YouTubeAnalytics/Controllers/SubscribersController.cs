using System.Threading.Tasks;
using System.Web.Mvc;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Areas.YouTubeAnalytics.Controllers
{
    public sealed class SubscribersController : AnalyticsReportControllerBase
    {
        public Task<ActionResult> Index()
        {
            return ReportAsync(
                AnalyticsReportKind.Subscribers,
                "subscribers",
                "Audience growth",
                "Identify the videos that turn viewers into a lasting audience.");
        }
    }
}
