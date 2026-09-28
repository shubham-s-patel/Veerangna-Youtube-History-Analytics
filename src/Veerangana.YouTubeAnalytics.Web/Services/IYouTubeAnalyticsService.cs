using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Services
{
    public interface IYouTubeAnalyticsService
    {
        Task<AnalyticsSummary> GetSummaryAsync(
            AnalyticsPeriod period,
            string countryCode,
            string contentType);

        Task<IList<AnalyticsTrendPoint>> GetTrendAsync(
            AnalyticsPeriod period,
            string countryCode,
            string contentType);

        Task<IList<VideoAnalyticsMetric>> GetTopVideosAsync(
            AnalyticsPeriod period,
            string countryCode,
            string contentType,
            int maxResults);

        Task<IList<AnalyticsBreakdownMetric>> GetTrafficSourcesAsync(
            AnalyticsPeriod period,
            string countryCode,
            string contentType,
            int maxResults);

        Task<IList<AnalyticsBreakdownMetric>> GetCountriesAsync(
            AnalyticsPeriod period,
            string countryCode,
            string contentType,
            int maxResults);

        Task<IList<DailyChannelAnalyticsMetric>> GetDailyChannelAnalyticsAsync(
            AnalyticsPeriod period);

        Task<IList<DailyChannelAnalyticsMetric>> GetCountryAnalyticsAsync(DateTime date);

        Task<IList<DailyTrafficSourceAnalyticsMetric>> GetDailyTrafficSourcesAsync(
            AnalyticsPeriod period,
            string countryCode);
    }
}
