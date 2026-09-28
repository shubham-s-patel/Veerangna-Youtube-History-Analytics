using System.Threading.Tasks;
using System.Web.Mvc;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Areas.YouTubeAnalytics.Controllers
{
    public sealed class GeographyController : AnalyticsReportControllerBase
    {
        public Task<ActionResult> Index()
        {
            return ReportAsync(
                AnalyticsReportKind.Geography,
                "geography",
                "Markets",
                "Compare video consumption and watch quality across countries.");
        }
    }
}
