using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Areas.YouTubeAnalytics.Controllers
{
    public sealed class VideosController : AnalyticsReportControllerBase
    {
        private static readonly Regex VideoIdPattern = new Regex(
            "^[A-Za-z0-9_-]{6,20}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public Task<ActionResult> Index()
        {
            return ReportAsync(
                AnalyticsReportKind.Videos,
                "videos",
                "Video library",
                "Search and compare every video available for analysis.");
        }

        public ActionResult Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !VideoIdPattern.IsMatch(id))
            {
                return HttpNotFound();
            }

            return EmptyAnalyticsPage("videos", "Video analysis", "Understand how viewers consume and respond to this video.");
        }
    }
}
