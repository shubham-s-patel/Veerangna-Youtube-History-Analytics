using System.Collections.Generic;
using Veerangana.YouTubeAnalytics.ViewModels;

namespace Veerangana.YouTubeAnalytics.Services
{
    public interface INavigationService
    {
        IList<NavigationItemViewModel> GetItems(string activeKey);
    }
}
