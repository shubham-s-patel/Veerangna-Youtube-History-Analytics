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
    public sealed class AnalyticsWorkspaceService : IAnalyticsWorkspaceService
    {
        private readonly IYouTubeDataService _dataService;
        private readonly IYouTubeAnalyticsService _analyticsService;
        private readonly IAnalyticsSnapshotRepository _snapshotRepository;

        public AnalyticsWorkspaceService(
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

        public async Task<AnalyticsReportData> GetReportAsync(
            AnalyticsReportKind reportKind,
            GlobalFilterViewModel filters)
        {
            if (filters == null) throw new ArgumentNullException("filters");

            YouTubeChannelInfo channel = await _dataService.GetAuthorizedChannelAsync().ConfigureAwait(false);
            AnalyticsPeriod period = BuildPeriod(filters, channel.PublishedAtUtc);
            int videoLimit = reportKind == AnalyticsReportKind.Videos ||
                             !string.IsNullOrWhiteSpace(filters.ContentType)
                ? 200
                : 15;

            Task<AnalyticsSummary> summaryTask = _analyticsService.GetSummaryAsync(
                period,
                filters.Country,
                filters.ContentType);
            Task<IList<AnalyticsTrendPoint>> trendTask = _analyticsService.GetTrendAsync(
                period,
                filters.Country,
                filters.ContentType);
            Task<IList<VideoAnalyticsMetric>> videosTask = RequiresVideos(reportKind)
                ? _analyticsService.GetTopVideosAsync(
                    period,
                    filters.Country,
                    filters.ContentType,
                    videoLimit)
                : Task.FromResult<IList<VideoAnalyticsMetric>>(new List<VideoAnalyticsMetric>());
            Task<IList<AnalyticsBreakdownMetric>> breakdownTask = GetBreakdownTask(
                reportKind,
                period,
                filters);
            Task<AnalyticsSummary> previousTask = filters.ComparePeriod && filters.DateRangeKey != "lifetime"
                ? _analyticsService.GetSummaryAsync(
                    PreviousPeriod(period),
                    filters.Country,
                    filters.ContentType)
                : Task.FromResult<AnalyticsSummary>(null);

            await Task.WhenAll(
                summaryTask,
                trendTask,
                videosTask,
                breakdownTask,
                previousTask).ConfigureAwait(false);

            IList<VideoAnalyticsMetric> videoMetrics = videosTask.Result;
            IList<YouTubeVideoInfo> videos = await _dataService
                .GetVideosAsync(videoMetrics.Select(metric => metric.VideoId))
                .ConfigureAwait(false);
            IDictionary<string, YouTubeVideoInfo> videosById = videos.ToDictionary(
                video => video.Id,
                StringComparer.Ordinal);

            IList<VideoUsageRecord> topVideos = videoMetrics
                .Where(metric => videosById.ContainsKey(metric.VideoId))
                .Where(metric => MatchesContentType(videosById[metric.VideoId], filters.ContentType))
                .Select(metric => new VideoUsageRecord
                {
                    Video = videosById[metric.VideoId],
                    Analytics = metric
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
                Trace.TraceError("Analytics report snapshot persistence failed: {0}", exception);
            }

            return new AnalyticsReportData
            {
                Channel = channel,
                Period = period,
                Current = summaryTask.Result,
                Previous = previousTask.Result,
                Trend = trendTask.Result,
                TopVideos = topVideos,
                Breakdown = breakdownTask.Result
            };
        }

        private Task<IList<AnalyticsBreakdownMetric>> GetBreakdownTask(
            AnalyticsReportKind reportKind,
            AnalyticsPeriod period,
            GlobalFilterViewModel filters)
        {
            if (reportKind == AnalyticsReportKind.TrafficSources)
            {
                return _analyticsService.GetTrafficSourcesAsync(
                    period,
                    filters.Country,
                    filters.ContentType,
                    25);
            }

            if (reportKind == AnalyticsReportKind.Geography)
            {
                return _analyticsService.GetCountriesAsync(
                    period,
                    filters.Country,
                    filters.ContentType,
                    50);
            }

            return Task.FromResult<IList<AnalyticsBreakdownMetric>>(
                new List<AnalyticsBreakdownMetric>());
        }

        private static bool RequiresVideos(AnalyticsReportKind reportKind)
        {
            return reportKind != AnalyticsReportKind.TrafficSources &&
                   reportKind != AnalyticsReportKind.Geography;
        }

        private static AnalyticsPeriod BuildPeriod(
            GlobalFilterViewModel filters,
            DateTime channelPublishedAtUtc)
        {
            DateTime maximumEndDate = DateTime.UtcNow.Date.AddDays(-1);
            DateTime endDate = maximumEndDate;
            DateTime startDate;

            switch (filters.DateRangeKey)
            {
                case "7d": startDate = endDate.AddDays(-6); break;
                case "90d": startDate = endDate.AddDays(-89); break;
                case "365d": startDate = endDate.AddDays(-364); break;
                case "lifetime": startDate = channelPublishedAtUtc.Date; break;
                case "custom":
                    startDate = filters.CustomStartDate.HasValue
                        ? filters.CustomStartDate.Value.Date
                        : endDate.AddDays(-27);
                    endDate = filters.CustomEndDate.HasValue
                        ? filters.CustomEndDate.Value.Date
                        : endDate;
                    break;
                default: startDate = endDate.AddDays(-27); break;
            }

            endDate = endDate > maximumEndDate ? maximumEndDate : endDate;
            startDate = startDate < channelPublishedAtUtc.Date ? channelPublishedAtUtc.Date : startDate;
            if (startDate > endDate) startDate = endDate;

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
