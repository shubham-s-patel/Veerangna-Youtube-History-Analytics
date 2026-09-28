using System;
using System.Collections.Generic;
using Veerangana.YouTubeAnalytics.ViewModels;

namespace Veerangana.YouTubeAnalytics.Services
{
    public sealed class DashboardNavigationService : INavigationService
    {
        public IList<NavigationItemViewModel> GetItems(string activeKey)
        {
            return new List<NavigationItemViewModel>
            {
                Create("overview", "Usage overview", "Overview", "fa fa-th-large", activeKey),
                Create("videos", "Video library", "Videos", "fa fa-play-circle", activeKey),
                Create("video-analytics", "Consumption", "VideoAnalytics", "fa fa-line-chart", activeKey),
                Create("audience", "Viewer behavior", "Audience", "fa fa-users", activeKey),
                Create("engagement", "Interactions", "Engagement", "fa fa-heart-o", activeKey),
                Create("traffic", "Discovery paths", "TrafficSources", "fa fa-random", activeKey),
                Create("geography", "Markets", "Geography", "fa fa-globe", activeKey),
                Create("subscribers", "Audience growth", "Subscribers", "fa fa-user-plus", activeKey),
                Create("reports", "Analysis reports", "Reports", "fa fa-file-text-o", activeKey),
                Create("settings", "Data sources", "Settings", "fa fa-database", activeKey)
            };
        }

        private static NavigationItemViewModel Create(
            string key,
            string label,
            string controller,
            string iconCssClass,
            string activeKey)
        {
            return new NavigationItemViewModel
            {
                Key = key,
                Label = label,
                Controller = controller,
                Action = "Index",
                IconCssClass = iconCssClass,
                IsActive = string.Equals(key, activeKey, StringComparison.OrdinalIgnoreCase)
            };
        }
    }
}
