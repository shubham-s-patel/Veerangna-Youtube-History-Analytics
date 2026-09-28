using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTubeAnalytics.v2;
using Google.Apis.YouTubeAnalytics.v2.Data;
using Veerangana.YouTubeAnalytics.Infrastructure.Configuration;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Services
{
    public sealed class YouTubeAnalyticsService : IYouTubeAnalyticsService
    {
        private const string SummaryMetrics =
            "views,estimatedMinutesWatched,averageViewDuration,averageViewPercentage," +
            "likes,comments,shares,subscribersGained,subscribersLost";

        private readonly IYouTubeCredentialProvider _credentialProvider;
        private readonly IYouTubeConfiguration _configuration;

        public YouTubeAnalyticsService(
            IYouTubeCredentialProvider credentialProvider,
            IYouTubeConfiguration configuration)
        {
            if (credentialProvider == null)
            {
                throw new ArgumentNullException("credentialProvider");
            }

            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            _credentialProvider = credentialProvider;
            _configuration = configuration;
        }

        public async Task<AnalyticsSummary> GetSummaryAsync(
            AnalyticsPeriod period,
            string countryCode,
            string contentType)
        {
            QueryResponse response = await QueryAsync(
                period,
                SummaryMetrics,
                BuildDimensions(null, contentType),
                BuildFilters(countryCode),
                null,
                null).ConfigureAwait(false);

            return AggregateSummary(response, FilterByContentType(response, contentType));
        }

        public async Task<IList<AnalyticsTrendPoint>> GetTrendAsync(
            AnalyticsPeriod period,
            string countryCode,
            string contentType)
        {
            QueryResponse response = await QueryAsync(
                period,
                "views,estimatedMinutesWatched,subscribersGained,subscribersLost",
                BuildDimensions("day", contentType),
                BuildFilters(countryCode),
                "day",
                null).ConfigureAwait(false);

            return FilterByContentType(response, contentType)
                .Select(row => new AnalyticsTrendPoint
                {
                    Date = GetDate(response, row, "day"),
                    Views = GetInt64(response, row, "views"),
                    WatchTimeMinutes = GetDouble(response, row, "estimatedMinutesWatched"),
                    SubscribersGained = GetInt64(response, row, "subscribersGained"),
                    SubscribersLost = GetInt64(response, row, "subscribersLost")
                })
                .ToList();
        }

        public async Task<IList<VideoAnalyticsMetric>> GetTopVideosAsync(
            AnalyticsPeriod period,
            string countryCode,
            string contentType,
            int maxResults)
        {
            QueryResponse response = await QueryAsync(
                period,
                "views,estimatedMinutesWatched,averageViewDuration,averageViewPercentage," +
                "likes,comments,shares,subscribersGained,subscribersLost",
                "video",
                BuildFilters(countryCode),
                "-views",
                Math.Max(1, Math.Min(200, maxResults))).ConfigureAwait(false);

            return Rows(response)
                .Select(row => new VideoAnalyticsMetric
                {
                    VideoId = GetString(response, row, "video"),
                    Views = GetInt64(response, row, "views"),
                    WatchTimeMinutes = GetDouble(response, row, "estimatedMinutesWatched"),
                    AverageViewDurationSeconds = GetDouble(response, row, "averageViewDuration"),
                    AverageViewPercentage = GetDouble(response, row, "averageViewPercentage"),
                    Likes = GetInt64(response, row, "likes"),
                    Comments = GetInt64(response, row, "comments"),
                    Shares = GetInt64(response, row, "shares"),
                    SubscribersGained = GetInt64(response, row, "subscribersGained"),
                    SubscribersLost = GetInt64(response, row, "subscribersLost")
                })
                .ToList();
        }

        public Task<IList<AnalyticsBreakdownMetric>> GetTrafficSourcesAsync(
            AnalyticsPeriod period,
            string countryCode,
            string contentType,
            int maxResults)
        {
            return GetBreakdownAsync(
                period,
                countryCode,
                contentType,
                "insightTrafficSourceType",
                maxResults);
        }

        public Task<IList<AnalyticsBreakdownMetric>> GetCountriesAsync(
            AnalyticsPeriod period,
            string countryCode,
            string contentType,
            int maxResults)
        {
            return GetBreakdownAsync(period, countryCode, contentType, "country", maxResults);
        }

        public async Task<IList<DailyChannelAnalyticsMetric>> GetDailyChannelAnalyticsAsync(
            AnalyticsPeriod period)
        {
            QueryResponse response = await QueryAsync(
                period,
                SummaryMetrics,
                "day,creatorContentType",
                null,
                "day",
                null).ConfigureAwait(false);

            return Rows(response).Select(row => new DailyChannelAnalyticsMetric
            {
                Date = GetDate(response, row, "day"),
                CountryCode = string.Empty,
                ContentType = MapApiContentType(GetString(response, row, "creatorContentType")),
                Metrics = ToSummary(response, row)
            }).ToList();
        }

        public async Task<IList<DailyChannelAnalyticsMetric>> GetCountryAnalyticsAsync(DateTime date)
        {
            var period = new AnalyticsPeriod { StartDate = date.Date, EndDate = date.Date };
            QueryResponse response = await QueryAsync(
                period,
                SummaryMetrics,
                "country,creatorContentType",
                null,
                "-views",
                250).ConfigureAwait(false);

            return Rows(response).Select(row => new DailyChannelAnalyticsMetric
            {
                Date = date.Date,
                CountryCode = GetString(response, row, "country").ToUpperInvariant(),
                ContentType = MapApiContentType(GetString(response, row, "creatorContentType")),
                Metrics = ToSummary(response, row)
            }).ToList();
        }

        public async Task<IList<DailyTrafficSourceAnalyticsMetric>> GetDailyTrafficSourcesAsync(
            AnalyticsPeriod period,
            string countryCode)
        {
            QueryResponse response = await QueryAsync(
                period,
                "views,estimatedMinutesWatched,averageViewDuration,averageViewPercentage",
                "day,insightTrafficSourceType,creatorContentType",
                BuildFilters(countryCode),
                null,
                null).ConfigureAwait(false);

            return Rows(response).Select(row => new DailyTrafficSourceAnalyticsMetric
            {
                Date = GetDate(response, row, "day"),
                CountryCode = (countryCode ?? string.Empty).ToUpperInvariant(),
                ContentType = MapApiContentType(GetString(response, row, "creatorContentType")),
                TrafficSource = GetString(response, row, "insightTrafficSourceType"),
                Views = GetInt64(response, row, "views"),
                WatchTimeMinutes = GetDouble(response, row, "estimatedMinutesWatched"),
                AverageViewDurationSeconds = GetDouble(response, row, "averageViewDuration"),
                AverageViewPercentage = GetDouble(response, row, "averageViewPercentage")
            }).ToList();
        }

        private async Task<IList<AnalyticsBreakdownMetric>> GetBreakdownAsync(
            AnalyticsPeriod period,
            string countryCode,
            string contentType,
            string dimension,
            int maxResults)
        {
            QueryResponse response = await QueryAsync(
                period,
                "views,estimatedMinutesWatched,averageViewDuration,averageViewPercentage",
                BuildDimensions(dimension, contentType),
                BuildFilters(countryCode),
                "-views",
                Math.Max(1, Math.Min(200, maxResults))).ConfigureAwait(false);

            return FilterByContentType(response, contentType)
                .Select(row => new AnalyticsBreakdownMetric
                {
                    Key = GetString(response, row, dimension),
                    Views = GetInt64(response, row, "views"),
                    WatchTimeMinutes = GetDouble(response, row, "estimatedMinutesWatched"),
                    AverageViewDurationSeconds = GetDouble(response, row, "averageViewDuration"),
                    AverageViewPercentage = GetDouble(response, row, "averageViewPercentage")
                })
                .ToList();
        }

        private async Task<QueryResponse> QueryAsync(
            AnalyticsPeriod period,
            string metrics,
            string dimensions,
            string filters,
            string sort,
            int? maxResults)
        {
            UserCredential credential = await _credentialProvider.GetCredentialAsync().ConfigureAwait(false);
            using (var service = new Google.Apis.YouTubeAnalytics.v2.YouTubeAnalyticsService(
                new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _configuration.ApplicationName
            }))
            {
                ReportsResource.QueryRequest request = service.Reports.Query();
                request.Ids = "channel==MINE";
                request.StartDate = period.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                request.EndDate = period.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                request.Metrics = metrics;
                request.Dimensions = dimensions;
                request.Filters = filters;
                request.Sort = sort;
                request.MaxResults = maxResults;

                return await request.ExecuteAsync().ConfigureAwait(false);
            }
        }

        private static string BuildFilters(string countryCode)
        {
            return string.IsNullOrWhiteSpace(countryCode)
                ? null
                : "country==" + countryCode.ToUpperInvariant();
        }

        private static string BuildDimensions(string dimension, string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return dimension;
            }

            return string.IsNullOrWhiteSpace(dimension)
                ? "creatorContentType"
                : dimension + ",creatorContentType";
        }

        private static IEnumerable<IList<object>> FilterByContentType(
            QueryResponse response,
            string contentType)
        {
            string expected = MapContentType(contentType);
            return string.IsNullOrWhiteSpace(expected)
                ? Rows(response)
                : Rows(response).Where(row => string.Equals(
                    GetString(response, row, "creatorContentType"),
                    expected,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static AnalyticsSummary AggregateSummary(
            QueryResponse response,
            IEnumerable<IList<object>> rows)
        {
            IList<IList<object>> selectedRows = rows.ToList();
            long views = selectedRows.Sum(row => GetInt64(response, row, "views"));
            double weightedDuration = selectedRows.Sum(row =>
                GetDouble(response, row, "averageViewDuration") * GetInt64(response, row, "views"));
            double weightedPercentage = selectedRows.Sum(row =>
                GetDouble(response, row, "averageViewPercentage") * GetInt64(response, row, "views"));

            return new AnalyticsSummary
            {
                Views = views,
                WatchTimeMinutes = selectedRows.Sum(row => GetDouble(response, row, "estimatedMinutesWatched")),
                AverageViewDurationSeconds = views == 0 ? 0d : weightedDuration / views,
                AverageViewPercentage = views == 0 ? 0d : weightedPercentage / views,
                Likes = selectedRows.Sum(row => GetInt64(response, row, "likes")),
                Comments = selectedRows.Sum(row => GetInt64(response, row, "comments")),
                Shares = selectedRows.Sum(row => GetInt64(response, row, "shares")),
                SubscribersGained = selectedRows.Sum(row => GetInt64(response, row, "subscribersGained")),
                SubscribersLost = selectedRows.Sum(row => GetInt64(response, row, "subscribersLost"))
            };
        }

        private static AnalyticsSummary ToSummary(QueryResponse response, IList<object> row)
        {
            return new AnalyticsSummary
            {
                Views = GetInt64(response, row, "views"),
                WatchTimeMinutes = GetDouble(response, row, "estimatedMinutesWatched"),
                AverageViewDurationSeconds = GetDouble(response, row, "averageViewDuration"),
                AverageViewPercentage = GetDouble(response, row, "averageViewPercentage"),
                Likes = GetInt64(response, row, "likes"),
                Comments = GetInt64(response, row, "comments"),
                Shares = GetInt64(response, row, "shares"),
                SubscribersGained = GetInt64(response, row, "subscribersGained"),
                SubscribersLost = GetInt64(response, row, "subscribersLost")
            };
        }

        private static string MapApiContentType(string contentType)
        {
            switch ((contentType ?? string.Empty).ToUpperInvariant())
            {
                case "SHORTS": return "Short";
                case "LIVE_STREAM": return "Live";
                case "VIDEO_ON_DEMAND": return "Video";
                case "STORY": return "Story";
                default: return "Unspecified";
            }
        }

        private static string MapContentType(string contentType)
        {
            switch ((contentType ?? string.Empty).ToLowerInvariant())
            {
                case "short": return "SHORTS";
                case "live": return "LIVE_STREAM";
                case "video": return "VIDEO_ON_DEMAND";
                default: return null;
            }
        }

        private static IList<IList<object>> Rows(QueryResponse response)
        {
            return response == null || response.Rows == null
                ? new List<IList<object>>()
                : response.Rows;
        }

        private static IList<object> FirstRow(QueryResponse response)
        {
            return Rows(response).FirstOrDefault() ?? new List<object>();
        }

        private static int ColumnIndex(QueryResponse response, string columnName)
        {
            if (response == null || response.ColumnHeaders == null)
            {
                return -1;
            }

            for (int index = 0; index < response.ColumnHeaders.Count; index++)
            {
                if (string.Equals(
                    response.ColumnHeaders[index].Name,
                    columnName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static object Value(QueryResponse response, IList<object> row, string columnName)
        {
            int index = ColumnIndex(response, columnName);
            return index >= 0 && index < row.Count ? row[index] : null;
        }

        private static string GetString(QueryResponse response, IList<object> row, string columnName)
        {
            object value = Value(response, row, columnName);
            return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static long GetInt64(QueryResponse response, IList<object> row, string columnName)
        {
            object value = Value(response, row, columnName);
            return value == null ? 0L : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static double GetDouble(QueryResponse response, IList<object> row, string columnName)
        {
            object value = Value(response, row, columnName);
            return value == null ? 0d : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static DateTime GetDate(QueryResponse response, IList<object> row, string columnName)
        {
            DateTime result;
            return DateTime.TryParseExact(
                GetString(response, row, columnName),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result)
                ? result
                : DateTime.MinValue;
        }
    }
}
