using System.Web.Mvc;

namespace Veerangana.YouTubeAnalytics.Controllers
{
    public sealed class HomeController : Controller
    {
        public ActionResult Index()
        {
            return RedirectToAction("Index", "Overview", new { area = "YouTubeAnalytics" });
        }
    }
}
