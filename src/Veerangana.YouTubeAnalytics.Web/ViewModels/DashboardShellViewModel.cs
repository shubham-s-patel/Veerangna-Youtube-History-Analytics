using System;
using System.Collections.Generic;

namespace Veerangana.YouTubeAnalytics.ViewModels
{
    public class DashboardShellViewModel
    {
        public DashboardShellViewModel()
        {
            Navigation = new List<NavigationItemViewModel>();
            Filters = new GlobalFilterViewModel();
        }

        public string PageTitle { get; set; }

        public string PageDescription { get; set; }

        public string ActiveNavigationKey { get; set; }

        public string WorkspaceName { get; set; }

        public string WorkspaceAvatarUrl { get; set; }

        public DateTime LastUpdatedUtc { get; set; }

        public bool IsMockMode { get; set; }

        public IList<NavigationItemViewModel> Navigation { get; set; }

        public GlobalFilterViewModel Filters { get; set; }

        public DatabaseStatusViewModel DatabaseStatus { get; set; }

        public YouTubeConfigurationStatusViewModel YouTubeConfigurationStatus { get; set; }
    }
}
