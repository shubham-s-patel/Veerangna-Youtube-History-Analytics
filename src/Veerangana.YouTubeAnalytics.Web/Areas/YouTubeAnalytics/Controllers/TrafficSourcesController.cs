using System.Threading.Tasks;
using System.Web.Mvc;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Areas.YouTubeAnalytics.Controllers
{
    public sealed class TrafficSourcesController : AnalyticsReportControllerBase
    {
        public Task<ActionResult> Index()
        {
            return ReportAsync(
                AnalyticsReportKind.TrafficSources,
                "traffic",
                "Discovery paths",
                "See where viewers encounter videos and which paths sustain attention.");
        }
    }
}
