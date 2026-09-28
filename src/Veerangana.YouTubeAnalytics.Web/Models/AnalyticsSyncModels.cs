using System;
using System.Collections.Generic;

namespace Veerangana.YouTubeAnalytics.Models
{
    public sealed class DailyChannelAnalyticsMetric
    {
        public DateTime Date { get; set; }

        public string CountryCode { get; set; }

        public string ContentType { get; set; }

        public AnalyticsSummary Metrics { get; set; }
    }

    public sealed class DailyVideoAnalyticsMetric
    {
        public DateTime Date { get; set; }

        public string CountryCode { get; set; }

        public VideoAnalyticsMetric Metrics { get; set; }
    }

    public sealed class DailyTrafficSourceAnalyticsMetric
    {
        public DateTime Date { get; set; }

        public string CountryCode { get; set; }

        public string ContentType { get; set; }

        public string TrafficSource { get; set; }

        public long Views { get; set; }

        public double WatchTimeMinutes { get; set; }

        public double AverageViewDurationSeconds { get; set; }

        public double AverageViewPercentage { get; set; }
    }

    public sealed class AnalyticsSyncLease
    {
        public long LogId { get; set; }

        public DateTime? OldestSyncedDate { get; set; }

        public DateTime? LatestSyncedDate { get; set; }

        public DateTime? OldestCountryVideoDate { get; set; }
    }

    public sealed class AnalyticsSyncBatch
    {
        public AnalyticsSyncBatch()
        {
            Videos = new List<YouTubeVideoInfo>();
            ChannelDaily = new List<DailyChannelAnalyticsMetric>();
            VideoDaily = new List<DailyVideoAnalyticsMetric>();
            TrafficSourceDaily = new List<DailyTrafficSourceAnalyticsMetric>();
            Periods = new List<AnalyticsPeriod>();
        }

        public YouTubeChannelInfo Channel { get; set; }

        public IList<YouTubeVideoInfo> Videos { get; set; }

        public IList<DailyChannelAnalyticsMetric> ChannelDaily { get; set; }

        public IList<DailyVideoAnalyticsMetric> VideoDaily { get; set; }

        public IList<DailyTrafficSourceAnalyticsMetric> TrafficSourceDaily { get; set; }

        public IList<AnalyticsPeriod> Periods { get; set; }

        public int RecordCount
        {
            get
            {
                return Videos.Count + ChannelDaily.Count + VideoDaily.Count + TrafficSourceDaily.Count;
            }
        }
    }

    public sealed class AnalyticsSyncResult
    {
        public bool Started { get; set; }

        public int RecordsProcessed { get; set; }

        public DateTime? OldestSyncedDate { get; set; }

        public DateTime? LatestSyncedDate { get; set; }
    }
}
