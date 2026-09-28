namespace Veerangana.YouTubeAnalytics.ViewModels
{
    public sealed class NavigationItemViewModel
    {
        public string Key { get; set; }

        public string Label { get; set; }

        public string Controller { get; set; }

        public string Action { get; set; }

        public string IconCssClass { get; set; }

        public bool IsActive { get; set; }
    }
}
