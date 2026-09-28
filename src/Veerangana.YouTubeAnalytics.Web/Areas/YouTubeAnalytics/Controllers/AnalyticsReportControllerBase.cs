using System;
using System.Configuration;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web.Mvc;
using Google;
using Google.Apis.Auth.OAuth2.Responses;
using Veerangana.YouTubeAnalytics.Infrastructure.Data;
using Veerangana.YouTubeAnalytics.Models;
using Veerangana.YouTubeAnalytics.Repositories;
using Veerangana.YouTubeAnalytics.Services;
using Veerangana.YouTubeAnalytics.ViewModels;

namespace Veerangana.YouTubeAnalytics.Areas.YouTubeAnalytics.Controllers
{
    public abstract class AnalyticsReportControllerBase : AnalyticsControllerBase
    {
        private readonly IAnalyticsWorkspaceService _workspaceService;

        protected AnalyticsReportControllerBase()
            : this(CreateWorkspaceService())
        {
        }

        protected AnalyticsReportControllerBase(IAnalyticsWorkspaceService workspaceService)
        {
            if (workspaceService == null) throw new ArgumentNullException("workspaceService");
            _workspaceService = workspaceService;
        }

        protected async Task<ActionResult> ReportAsync(
            AnalyticsReportKind reportKind,
            string activeNavigationKey,
            string pageTitle,
            string pageDescription)
        {
            AnalyticsReportViewModel model = BuildShell<AnalyticsReportViewModel>(
                activeNavigationKey,
                pageTitle,
                pageDescription);

            try
            {
                AnalyticsReportData data = await _workspaceService
                    .GetReportAsync(reportKind, model.Filters)
                    .ConfigureAwait(false);
                AnalyticsReportPresenter.Populate(model, data, reportKind);
            }
            catch (ConfigurationErrorsException exception)
            {
                Trace.TraceError("YouTube report configuration error: {0}", exception);
                model.ErrorMessage = "YouTube access is not fully configured. Check the data source settings.";
            }
            catch (TokenResponseException exception)
            {
                Trace.TraceError("YouTube report OAuth refresh failed: {0}", exception);
                model.ErrorMessage = "YouTube authorization has expired or was revoked. Add a new refresh token.";
            }
            catch (GoogleApiException exception)
            {
                Trace.TraceError("YouTube report API request failed: {0}", exception);
                model.ErrorMessage = "YouTube could not return this report. Verify API access and try again.";
            }
            catch (Exception exception)
            {
                Trace.TraceError("Analytics report failed: {0}", exception);
                model.ErrorMessage = "This analytics report could not be loaded. Try refreshing in a moment.";
            }

            return View(
                "~/Areas/YouTubeAnalytics/Views/Shared/_AnalyticsReport.cshtml",
                model);
        }

        private static IAnalyticsWorkspaceService CreateWorkspaceService()
        {
            return new SqlAnalyticsWorkspaceService(
                new AnalyticsFactRepository(new SqlConnectionFactory()));
        }
    }
}
