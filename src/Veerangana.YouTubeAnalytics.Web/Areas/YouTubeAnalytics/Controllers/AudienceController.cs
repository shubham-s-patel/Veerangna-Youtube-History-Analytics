using System.Threading.Tasks;
using System.Web.Mvc;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Areas.YouTubeAnalytics.Controllers
{
    public sealed class AudienceController : AnalyticsReportControllerBase
    {
        public Task<ActionResult> Index()
        {
            return ReportAsync(
                AnalyticsReportKind.Audience,
                "audience",
                "Viewer behavior",
                "Understand viewing depth, audience contribution and the videos people choose.");
        }
    }
}
