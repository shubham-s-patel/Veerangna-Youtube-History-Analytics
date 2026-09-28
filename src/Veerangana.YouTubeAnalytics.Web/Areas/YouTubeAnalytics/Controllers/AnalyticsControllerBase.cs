using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Mvc;
using Veerangana.YouTubeAnalytics.Helpers;
using Veerangana.YouTubeAnalytics.Services;
using Veerangana.YouTubeAnalytics.ViewModels;

namespace Veerangana.YouTubeAnalytics.Areas.YouTubeAnalytics.Controllers
{
    public abstract class AnalyticsControllerBase : Controller
    {
        private readonly INavigationService _navigationService;

        protected AnalyticsControllerBase()
            : this(new DashboardNavigationService())
        {
        }

        protected AnalyticsControllerBase(INavigationService navigationService)
        {
            if (navigationService == null)
            {
                throw new ArgumentNullException("navigationService");
            }

            _navigationService = navigationService;
        }

        protected DashboardShellViewModel BuildShell(
            string activeNavigationKey,
            string pageTitle,
            string pageDescription)
        {
            return BuildShell<DashboardShellViewModel>(
                activeNavigationKey,
                pageTitle,
                pageDescription);
        }

        protected T BuildShell<T>(
            string activeNavigationKey,
            string pageTitle,
            string pageDescription)
            where T : DashboardShellViewModel, new()
        {
            string rangeKey = DateRangeHelper.NormalizeKey(Request.QueryString["range"]);
            bool comparePeriod = ParseBoolean(Request.QueryString["compare"]);

            var filters = new GlobalFilterViewModel
            {
                DateRangeKey = rangeKey,
                CustomStartDate = DateRangeHelper.ParseIsoDate(Request.QueryString["start"]),
                CustomEndDate = DateRangeHelper.ParseIsoDate(Request.QueryString["end"]),
                ComparePeriod = comparePeriod,
                ContentType = NormalizeContentType(Request.QueryString["contentType"]),
                Country = NormalizeCountry(Request.QueryString["country"])
            };

            filters.DateRanges.Add(new DateRangeOptionViewModel { Value = "7d", Label = "Last 7 days" });
            filters.DateRanges.Add(new DateRangeOptionViewModel { Value = "28d", Label = "Last 28 days" });
            filters.DateRanges.Add(new DateRangeOptionViewModel { Value = "90d", Label = "Last 90 days" });
            filters.DateRanges.Add(new DateRangeOptionViewModel { Value = "365d", Label = "Last 365 days" });
            filters.DateRanges.Add(new DateRangeOptionViewModel { Value = "lifetime", Label = "Lifetime" });
            filters.DateRanges.Add(new DateRangeOptionViewModel { Value = "custom", Label = "Custom range" });

            filters.ContentTypes.Add(new FilterOptionViewModel { Value = string.Empty, Label = "All content" });
            filters.ContentTypes.Add(new FilterOptionViewModel { Value = "video", Label = "Videos" });
            filters.ContentTypes.Add(new FilterOptionViewModel { Value = "short", Label = "Shorts" });
            filters.ContentTypes.Add(new FilterOptionViewModel { Value = "live", Label = "Live streams" });

            bool useMockData;
            bool.TryParse(ConfigurationManager.AppSettings["UseMockYouTubeData"], out useMockData);

            ViewBag.Title = pageTitle;

            return new T
            {
                PageTitle = pageTitle,
                PageDescription = pageDescription,
                ActiveNavigationKey = activeNavigationKey,
                WorkspaceName = "Veerangana library",
                LastUpdatedUtc = DateTime.UtcNow,
                IsMockMode = useMockData,
                Navigation = _navigationService.GetItems(activeNavigationKey),
                Filters = filters
            };
        }

        protected ActionResult EmptyAnalyticsPage(
            string activeNavigationKey,
            string pageTitle,
            string pageDescription)
        {
            return View(
                "~/Areas/YouTubeAnalytics/Views/Shared/_EmptyAnalytics.cshtml",
                BuildShell(activeNavigationKey, pageTitle, pageDescription));
        }

        private static string NormalizeContentType(string contentType)
        {
            var validValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "video",
                "short",
                "live"
            };

            return !string.IsNullOrWhiteSpace(contentType) && validValues.Contains(contentType)
                ? contentType.ToLowerInvariant()
                : string.Empty;
        }

        private static string NormalizeCountry(string country)
        {
            var validValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "IN",
                "US",
                "GB",
                "CA"
            };

            return !string.IsNullOrWhiteSpace(country) && validValues.Contains(country)
                ? country.ToUpperInvariant()
                : string.Empty;
        }

        private static bool ParseBoolean(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] values = value.Split(',');
            foreach (string candidate in values)
            {
                if (string.Equals(candidate, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
