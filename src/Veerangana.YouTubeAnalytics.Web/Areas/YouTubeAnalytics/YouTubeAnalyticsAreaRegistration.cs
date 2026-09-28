using System.Web.Mvc;

namespace Veerangana.YouTubeAnalytics.Areas.YouTubeAnalytics
{
    public sealed class YouTubeAnalyticsAreaRegistration : AreaRegistration
    {
        public override string AreaName
        {
            get { return "YouTubeAnalytics"; }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                name: "YouTubeAnalytics_root",
                url: "youtube-analytics",
                defaults: new { controller = "Overview", action = "Index" },
                namespaces: new[] { "Veerangana.YouTubeAnalytics.Areas.YouTubeAnalytics.Controllers" });

            context.MapRoute(
                name: "YouTubeAnalytics_default",
                url: "youtube-analytics/{controller}/{action}/{id}",
                defaults: new { action = "Index", id = UrlParameter.Optional },
                namespaces: new[] { "Veerangana.YouTubeAnalytics.Areas.YouTubeAnalytics.Controllers" });
        }
    }
}
