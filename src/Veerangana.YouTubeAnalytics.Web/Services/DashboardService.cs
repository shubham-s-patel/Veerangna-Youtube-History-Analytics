using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Models;
using Veerangana.YouTubeAnalytics.Repositories;
using Veerangana.YouTubeAnalytics.ViewModels;

namespace Veerangana.YouTubeAnalytics.Services
{
    public sealed class DashboardService : IDashboardService
    {
        private readonly IYouTubeDataService _dataService;
        private readonly IYouTubeAnalyticsService _analyticsService;
        private readonly IAnalyticsSnapshotRepository _snapshotRepository;

        public DashboardService(
            IYouTubeDataService dataService,
            IYouTubeAnalyticsService analyticsService,
            IAnalyticsSnapshotRepository snapshotRepository)
        {
            if (dataService == null) throw new ArgumentNullException("dataService");
            if (analyticsService == null) throw new ArgumentNullException("analyticsService");
            if (snapshotRepository == null) throw new ArgumentNullException("snapshotRepository");

            _dataService = dataService;
            _analyticsService = analyticsService;
            _snapshotRepository = snapshotRepository;
        }

        public async Task<DashboardOverviewData> GetOverviewAsync(GlobalFilterViewModel filters)
        {
            if (filters == null) throw new ArgumentNullException("filters");

            YouTubeChannelInfo channel = await _dataService.GetAuthorizedChannelAsync().ConfigureAwait(false);
            AnalyticsPeriod period = BuildPeriod(filters, channel.PublishedAtUtc);

            Task<AnalyticsSummary> summaryTask = _analyticsService.GetSummaryAsync(
                period,
                filters.Country,
                filters.ContentType);
            Task<IList<AnalyticsTrendPoint>> trendTask = _analyticsService.GetTrendAsync(
                period,
                filters.Country,
                filters.ContentType);
            Task<IList<VideoAnalyticsMetric>> topVideosTask =
                _analyticsService.GetTopVideosAsync(
                    period,
                    filters.Country,
                    filters.ContentType,
                    string.IsNullOrWhiteSpace(filters.ContentType) ? 10 : 100);

            Task<AnalyticsSummary> previousTask = filters.ComparePeriod && filters.DateRangeKey != "lifetime"
                ? _analyticsService.GetSummaryAsync(
                    PreviousPeriod(period),
                    filters.Country,
                    filters.ContentType)
                : Task.FromResult<AnalyticsSummary>(null);

            await Task.WhenAll(summaryTask, trendTask, topVideosTask, previousTask).ConfigureAwait(false);

            IList<VideoAnalyticsMetric> videoMetrics = topVideosTask.Result;
            IList<YouTubeVideoInfo> videos = await _dataService
                .GetVideosAsync(videoMetrics.Select(metric => metric.VideoId))
                .ConfigureAwait(false);

            IDictionary<string, YouTubeVideoInfo> videosById = videos.ToDictionary(
                video => video.Id,
                StringComparer.Ordinal);

            var topVideos = videoMetrics
                .Where(metric => videosById.ContainsKey(metric.VideoId))
                .Where(metric => MatchesContentType(videosById[metric.VideoId], filters.ContentType))
                .Take(10)
                .Select(metric => new VideoUsageRecord
                {
                    Analytics = metric,
                    Video = videosById[metric.VideoId]
                })
                .ToList();

            try
            {
                await _snapshotRepository
                    .SaveOverviewSnapshotAsync(channel, videos, DateTime.UtcNow.Date)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Trace.TraceError("Analytics snapshot persistence failed: {0}", exception);
            }

            return new DashboardOverviewData
            {
                Channel = channel,
                Period = period,
                Current = summaryTask.Result,
                Previous = previousTask.Result,
                Trend = trendTask.Result,
                TopVideos = topVideos
            };
        }

        private static AnalyticsPeriod BuildPeriod(GlobalFilterViewModel filters, DateTime channelPublishedAtUtc)
        {
            DateTime endDate = DateTime.UtcNow.Date.AddDays(-1);
            DateTime startDate;

            switch (filters.DateRangeKey)
            {
                case "7d":
                    startDate = endDate.AddDays(-6);
                    break;
                case "90d":
                    startDate = endDate.AddDays(-89);
                    break;
                case "365d":
                    startDate = endDate.AddDays(-364);
                    break;
                case "lifetime":
                    startDate = channelPublishedAtUtc.Date;
                    break;
                case "custom":
                    startDate = filters.CustomStartDate.HasValue
                        ? filters.CustomStartDate.Value.Date
                        : endDate.AddDays(-27);
                    endDate = filters.CustomEndDate.HasValue
                        ? filters.CustomEndDate.Value.Date
                        : endDate;
                    break;
                default:
                    startDate = endDate.AddDays(-27);
                    break;
            }

            endDate = endDate > DateTime.UtcNow.Date.AddDays(-1)
                ? DateTime.UtcNow.Date.AddDays(-1)
                : endDate;
            startDate = startDate < channelPublishedAtUtc.Date ? channelPublishedAtUtc.Date : startDate;

            if (startDate > endDate)
            {
                startDate = endDate;
            }

            return new AnalyticsPeriod { StartDate = startDate, EndDate = endDate };
        }

        private static AnalyticsPeriod PreviousPeriod(AnalyticsPeriod current)
        {
            DateTime previousEnd = current.StartDate.AddDays(-1);
            return new AnalyticsPeriod
            {
                StartDate = previousEnd.AddDays(-(current.DayCount - 1)),
                EndDate = previousEnd
            };
        }

        private static bool MatchesContentType(YouTubeVideoInfo video, string contentType)
        {
            switch ((contentType ?? string.Empty).ToLowerInvariant())
            {
                case "short": return !video.IsLiveStream && video.DurationSeconds <= 180;
                case "live": return video.IsLiveStream;
                case "video": return !video.IsLiveStream && video.DurationSeconds > 180;
                default: return true;
            }
        }
    }
}
