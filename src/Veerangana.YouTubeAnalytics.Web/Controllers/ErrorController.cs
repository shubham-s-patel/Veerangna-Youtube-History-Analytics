using System.Web.Mvc;

namespace Veerangana.YouTubeAnalytics.Controllers
{
    public sealed class ErrorController : Controller
    {
        [AllowAnonymous]
        public ActionResult Index()
        {
            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;
            return View("~/Views/Shared/Error.cshtml");
        }
    }
}
