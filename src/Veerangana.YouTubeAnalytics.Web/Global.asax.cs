using System.Web.Mvc;
using System.Web.Routing;
using Veerangana.YouTubeAnalytics.Infrastructure.Data;
using Veerangana.YouTubeAnalytics.Services;

namespace Veerangana.YouTubeAnalytics
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            new AnalyticsDatabaseInitializer(new SqlConnectionFactory()).Initialize();
            YouTubeAnalyticsSyncScheduler.Start();
        }
    }
}
