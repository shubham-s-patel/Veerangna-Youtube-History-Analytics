using System.Threading.Tasks;
using System.Web.Mvc;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Areas.YouTubeAnalytics.Controllers
{
    public sealed class ReportsController : AnalyticsReportControllerBase
    {
        public Task<ActionResult> Index()
        {
            return ReportAsync(
                AnalyticsReportKind.Reports,
                "reports",
                "Analysis reports",
                "Review a focused period report around video use, attention and response.");
        }
    }
}
