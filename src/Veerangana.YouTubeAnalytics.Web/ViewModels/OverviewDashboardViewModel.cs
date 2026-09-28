using System.Collections.Generic;

namespace Veerangana.YouTubeAnalytics.ViewModels
{
    public sealed class OverviewDashboardViewModel : DashboardShellViewModel
    {
        public OverviewDashboardViewModel()
        {
            Metrics = new List<MetricCardViewModel>();
            Trend = new List<TrendPointViewModel>();
            TopVideos = new List<TopVideoViewModel>();
            Insights = new List<InsightViewModel>();
        }

        public bool HasData { get; set; }

        public string ErrorMessage { get; set; }

        public string PeriodLabel { get; set; }

        public IList<MetricCardViewModel> Metrics { get; set; }

        public IList<TrendPointViewModel> Trend { get; set; }

        public IList<TopVideoViewModel> TopVideos { get; set; }

        public IList<InsightViewModel> Insights { get; set; }
    }

    public sealed class MetricCardViewModel
    {
        public string Label { get; set; }

        public string Value { get; set; }

        public string Tooltip { get; set; }

        public string IconCssClass { get; set; }

        public string AccentCssClass { get; set; }

        public string ComparisonText { get; set; }

        public string ComparisonCssClass { get; set; }
    }

    public sealed class TrendPointViewModel
    {
        public string Label { get; set; }

        public long Views { get; set; }

        public double WatchTimeHours { get; set; }
    }

    public sealed class TopVideoViewModel
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string ThumbnailUrl { get; set; }

        public string PublishedDate { get; set; }

        public string Duration { get; set; }

        public string Views { get; set; }

        public string WatchTime { get; set; }

        public string Likes { get; set; }

        public string AudienceGained { get; set; }
    }

    public sealed class InsightViewModel
    {
        public string IconCssClass { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string AccentCssClass { get; set; }
    }
}
