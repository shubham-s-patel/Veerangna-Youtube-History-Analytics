using System;
using System.Collections.Generic;

namespace Veerangana.YouTubeAnalytics.Models
{
    public sealed class AnalyticsPeriod
    {
        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int DayCount
        {
            get { return (EndDate.Date - StartDate.Date).Days + 1; }
        }
    }

    public sealed class AnalyticsSummary
    {
        public long Views { get; set; }

        public double WatchTimeMinutes { get; set; }

        public double AverageViewDurationSeconds { get; set; }

        public double AverageViewPercentage { get; set; }

        public long Likes { get; set; }

        public long Comments { get; set; }

        public long Shares { get; set; }

        public long SubscribersGained { get; set; }

        public long SubscribersLost { get; set; }
    }

    public sealed class AnalyticsTrendPoint
    {
        public DateTime Date { get; set; }

        public long Views { get; set; }

        public double WatchTimeMinutes { get; set; }

        public long SubscribersGained { get; set; }

        public long SubscribersLost { get; set; }
    }

    public sealed class VideoAnalyticsMetric
    {
        public string VideoId { get; set; }

        public long Views { get; set; }

        public double WatchTimeMinutes { get; set; }

        public double AverageViewDurationSeconds { get; set; }

        public double AverageViewPercentage { get; set; }

        public long Likes { get; set; }

        public long Comments { get; set; }

        public long Shares { get; set; }

        public long SubscribersGained { get; set; }

        public long SubscribersLost { get; set; }
    }

    public sealed class AnalyticsBreakdownMetric
    {
        public string Key { get; set; }

        public long Views { get; set; }

        public double WatchTimeMinutes { get; set; }

        public double AverageViewDurationSeconds { get; set; }

        public double AverageViewPercentage { get; set; }
    }

    public enum AnalyticsReportKind
    {
        Videos,
        Consumption,
        Audience,
        Engagement,
        TrafficSources,
        Geography,
        Subscribers,
        Reports
    }

    public sealed class YouTubeChannelInfo
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string AvatarUrl { get; set; }

        public DateTime PublishedAtUtc { get; set; }

        public DateTime LastSyncedAtUtc { get; set; }

        public long ViewCount { get; set; }

        public long? SubscriberCount { get; set; }

        public int VideoCount { get; set; }
    }

    public sealed class YouTubeVideoInfo
    {
        public string Id { get; set; }

        public string ChannelId { get; set; }

        public string Title { get; set; }

        public string ThumbnailUrl { get; set; }

        public DateTime PublishedAtUtc { get; set; }

        public int DurationSeconds { get; set; }

        public bool IsLiveStream { get; set; }

        public long ViewCount { get; set; }

        public long LikeCount { get; set; }

        public long CommentCount { get; set; }
    }

    public sealed class VideoUsageRecord
    {
        public YouTubeVideoInfo Video { get; set; }

        public VideoAnalyticsMetric Analytics { get; set; }
    }

    public sealed class DashboardOverviewData
    {
        public DashboardOverviewData()
        {
            Trend = new List<AnalyticsTrendPoint>();
            TopVideos = new List<VideoUsageRecord>();
        }

        public YouTubeChannelInfo Channel { get; set; }

        public AnalyticsPeriod Period { get; set; }

        public AnalyticsSummary Current { get; set; }

        public AnalyticsSummary Previous { get; set; }

        public IList<AnalyticsTrendPoint> Trend { get; set; }

        public IList<VideoUsageRecord> TopVideos { get; set; }
    }

    public sealed class AnalyticsReportData
    {
        public AnalyticsReportData()
        {
            Trend = new List<AnalyticsTrendPoint>();
            TopVideos = new List<VideoUsageRecord>();
            Breakdown = new List<AnalyticsBreakdownMetric>();
        }

        public YouTubeChannelInfo Channel { get; set; }

        public AnalyticsPeriod Period { get; set; }

        public AnalyticsSummary Current { get; set; }

        public AnalyticsSummary Previous { get; set; }

        public IList<AnalyticsTrendPoint> Trend { get; set; }

        public IList<VideoUsageRecord> TopVideos { get; set; }

        public IList<AnalyticsBreakdownMetric> Breakdown { get; set; }
    }
}
