using System.Collections.Generic;

namespace Veerangana.YouTubeAnalytics.ViewModels
{
    public sealed class AnalyticsReportViewModel : DashboardShellViewModel
    {
        public AnalyticsReportViewModel()
        {
            Metrics = new List<MetricCardViewModel>();
            ChartPoints = new List<ReportChartPointViewModel>();
            Rows = new List<AnalyticsReportRowViewModel>();
            Insights = new List<InsightViewModel>();
        }

        public bool HasData { get; set; }

        public string ErrorMessage { get; set; }

        public string PeriodLabel { get; set; }

        public string SectionCssClass { get; set; }

        public IList<MetricCardViewModel> Metrics { get; set; }

        public string ChartKicker { get; set; }

        public string ChartTitle { get; set; }

        public string ChartType { get; set; }

        public string PrimarySeriesLabel { get; set; }

        public string SecondarySeriesLabel { get; set; }

        public IList<ReportChartPointViewModel> ChartPoints { get; set; }

        public string TableKicker { get; set; }

        public string TableTitle { get; set; }

        public string FirstColumnLabel { get; set; }

        public string Value1Label { get; set; }

        public string Value2Label { get; set; }

        public string Value3Label { get; set; }

        public string Value4Label { get; set; }

        public IList<AnalyticsReportRowViewModel> Rows { get; set; }

        public IList<InsightViewModel> Insights { get; set; }

        public string EmptyMessage { get; set; }
    }

    public sealed class ReportChartPointViewModel
    {
        public string Label { get; set; }

        public double PrimaryValue { get; set; }

        public double SecondaryValue { get; set; }
    }

    public sealed class AnalyticsReportRowViewModel
    {
        public string Label { get; set; }

        public string Subtitle { get; set; }

        public string ImageUrl { get; set; }

        public string LinkUrl { get; set; }

        public string Badge { get; set; }

        public string Value1 { get; set; }

        public string Value2 { get; set; }

        public string Value3 { get; set; }

        public string Value4 { get; set; }
    }
}
