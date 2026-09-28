using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Models;
using Veerangana.YouTubeAnalytics.Repositories;

namespace Veerangana.YouTubeAnalytics.Services
{
    public sealed class AnalyticsSyncService : IAnalyticsSyncService
    {
        private static readonly string[] SupportedCountries = { "IN", "US", "GB", "CA" };

        private readonly IYouTubeDataService _dataService;
        private readonly IYouTubeAnalyticsService _analyticsService;
        private readonly IAnalyticsFactRepository _repository;

        public AnalyticsSyncService(
            IYouTubeDataService dataService,
            IYouTubeAnalyticsService analyticsService,
            IAnalyticsFactRepository repository)
        {
            if (dataService == null) throw new ArgumentNullException("dataService");
            if (analyticsService == null) throw new ArgumentNullException("analyticsService");
            if (repository == null) throw new ArgumentNullException("repository");

            _dataService = dataService;
            _analyticsService = analyticsService;
            _repository = repository;
        }

        public async Task<AnalyticsSyncResult> RunAsync(string trigger)
        {
            int leaseMinutes = ReadInt("YouTubeSyncLeaseMinutes", 30, 5, 240);
            AnalyticsSyncLease lease = await _repository
                .TryBeginSyncAsync(trigger, TimeSpan.FromMinutes(leaseMinutes))
                .ConfigureAwait(false);

            if (lease == null)
            {
                return new AnalyticsSyncResult { Started = false };
            }

            try
            {
                YouTubeChannelInfo channel = await RetryAsync(
                    () => _dataService.GetAuthorizedChannelAsync()).ConfigureAwait(false);
                IList<AnalyticsPeriod> periods = BuildSyncPeriods(channel, lease);
                var batch = new AnalyticsSyncBatch { Channel = channel, Periods = periods };

                foreach (AnalyticsPeriod period in periods)
                {
                    await PopulatePeriodAsync(batch, period).ConfigureAwait(false);
                }

                string[] videoIds = batch.VideoDaily
                    .Select(item => item.Metrics.VideoId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                batch.Videos = await RetryAsync(
                    () => _dataService.GetVideosAsync(videoIds)).ConfigureAwait(false);

                var validVideoIds = new HashSet<string>(
                    batch.Videos.Select(video => video.Id),
                    StringComparer.Ordinal);
                batch.VideoDaily = batch.VideoDaily
                    .Where(metric => validVideoIds.Contains(metric.Metrics.VideoId))
                    .ToList();

                Consolidate(batch);

                await _repository.SaveSyncBatchAsync(batch).ConfigureAwait(false);
                DateTime oldest = periods.Min(period => period.StartDate);
                DateTime latest = periods.Max(period => period.EndDate);
                await _repository.CompleteSyncAsync(
                    lease,
                    batch.RecordCount,
                    oldest,
                    latest).ConfigureAwait(false);

                return new AnalyticsSyncResult
                {
                    Started = true,
                    RecordsProcessed = batch.RecordCount,
                    OldestSyncedDate = oldest,
                    LatestSyncedDate = latest
                };
            }
            catch (Exception exception)
            {
                await _repository.FailSyncAsync(lease, exception).ConfigureAwait(false);
                throw;
            }
        }

        private async Task PopulatePeriodAsync(AnalyticsSyncBatch batch, AnalyticsPeriod period)
        {
            Task<IList<DailyChannelAnalyticsMetric>> channelTask = RetryAsync(
                () => _analyticsService.GetDailyChannelAnalyticsAsync(period));

            var trafficTasks = new List<Task<IList<DailyTrafficSourceAnalyticsMetric>>>
            {
                RetryAsync(() => _analyticsService.GetDailyTrafficSourcesAsync(period, string.Empty))
            };
            trafficTasks.AddRange(SupportedCountries.Select(country => RetryAsync(
                () => _analyticsService.GetDailyTrafficSourcesAsync(period, country))));

            var videoMetrics = new List<DailyVideoAnalyticsMetric>();
            var countryMetrics = new List<DailyChannelAnalyticsMetric>();
            for (DateTime date = period.StartDate.Date; date <= period.EndDate.Date; date = date.AddDays(1))
            {
                DateTime metricDate = date;
                var videoTasks = new List<KeyValuePair<string, Task<IList<VideoAnalyticsMetric>>>>
                {
                    new KeyValuePair<string, Task<IList<VideoAnalyticsMetric>>>(
                        string.Empty,
                        LoadDailyVideosAsync(metricDate, string.Empty))
                };
                foreach (string supportedCountry in SupportedCountries)
                {
                    string countryCode = supportedCountry;
                    videoTasks.Add(new KeyValuePair<string, Task<IList<VideoAnalyticsMetric>>>(
                        countryCode,
                        LoadDailyVideosAsync(metricDate, countryCode)));
                }

                Task<IList<DailyChannelAnalyticsMetric>> countryTask = RetryAsync(
                    () => _analyticsService.GetCountryAnalyticsAsync(metricDate));

                await Task.WhenAll(
                    videoTasks.Select(item => (Task)item.Value).Concat(new Task[] { countryTask }))
                    .ConfigureAwait(false);
                foreach (KeyValuePair<string, Task<IList<VideoAnalyticsMetric>>> videoTask in videoTasks)
                {
                    videoMetrics.AddRange(videoTask.Value.Result.Select(metric => new DailyVideoAnalyticsMetric
                    {
                        Date = metricDate,
                        CountryCode = videoTask.Key,
                        Metrics = metric
                    }));
                }

                countryMetrics.AddRange(countryTask.Result);
            }

            await channelTask.ConfigureAwait(false);
            await Task.WhenAll(trafficTasks).ConfigureAwait(false);

            batch.ChannelDaily = batch.ChannelDaily
                .Concat(channelTask.Result)
                .Concat(countryMetrics)
                .ToList();
            batch.VideoDaily = batch.VideoDaily.Concat(videoMetrics).ToList();
            foreach (Task<IList<DailyTrafficSourceAnalyticsMetric>> task in trafficTasks)
            {
                batch.TrafficSourceDaily = batch.TrafficSourceDaily.Concat(task.Result).ToList();
            }
        }

        private Task<IList<VideoAnalyticsMetric>> LoadDailyVideosAsync(
            DateTime metricDate,
            string countryCode)
        {
            return RetryAsync(() => _analyticsService.GetTopVideosAsync(
                new AnalyticsPeriod { StartDate = metricDate, EndDate = metricDate },
                countryCode,
                string.Empty,
                200));
        }

        private static IList<AnalyticsPeriod> BuildSyncPeriods(
            YouTubeChannelInfo channel,
            AnalyticsSyncLease lease)
        {
            int analyticsLagDays = ReadInt("YouTubeSyncAnalyticsLagDays", 3, 1, 14);
            int initialDays = ReadInt("YouTubeSyncInitialDays", 28, 7, 90);
            int overlapDays = ReadInt("YouTubeSyncOverlapDays", 3, 1, 14);
            int historyDays = ReadInt("YouTubeSyncHistoryDays", 365, initialDays, 3650);
            int backfillChunkDays = ReadInt("YouTubeSyncBackfillChunkDays", 30, 7, 90);

            DateTime latestAvailable = DateTime.UtcNow.Date.AddDays(-analyticsLagDays);
            DateTime historyTarget = latestAvailable.AddDays(-(historyDays - 1));
            if (historyTarget < channel.PublishedAtUtc.Date) historyTarget = channel.PublishedAtUtc.Date;

            var periods = new List<AnalyticsPeriod>();
            if (!lease.OldestSyncedDate.HasValue)
            {
                periods.Add(new AnalyticsPeriod
                {
                    StartDate = Max(historyTarget, latestAvailable.AddDays(-(initialDays - 1))),
                    EndDate = latestAvailable
                });
                return periods;
            }

            periods.Add(new AnalyticsPeriod
            {
                StartDate = Max(historyTarget, latestAvailable.AddDays(-(overlapDays - 1))),
                EndDate = latestAvailable
            });

            if (!lease.OldestCountryVideoDate.HasValue)
            {
                periods.Add(new AnalyticsPeriod
                {
                    StartDate = Max(historyTarget, latestAvailable.AddDays(-(initialDays - 1))),
                    EndDate = latestAvailable
                });
                return MergePeriods(periods);
            }

            if (lease.OldestCountryVideoDate.Value.Date > lease.OldestSyncedDate.Value.Date)
            {
                DateTime countryVideoEnd = lease.OldestCountryVideoDate.Value.Date.AddDays(-1);
                if (countryVideoEnd >= historyTarget)
                {
                    periods.Add(new AnalyticsPeriod
                    {
                        StartDate = Max(historyTarget, countryVideoEnd.AddDays(-(backfillChunkDays - 1))),
                        EndDate = countryVideoEnd
                    });
                }

                return MergePeriods(periods);
            }

            DateTime historicalEnd = lease.OldestSyncedDate.Value.Date.AddDays(-1);
            if (historicalEnd >= historyTarget)
            {
                periods.Add(new AnalyticsPeriod
                {
                    StartDate = Max(historyTarget, historicalEnd.AddDays(-(backfillChunkDays - 1))),
                    EndDate = historicalEnd
                });
            }

            return MergePeriods(periods);
        }

        private static async Task<T> RetryAsync<T>(Func<Task<T>> operation)
        {
            int attempts = ReadInt("YouTubeSyncRetryCount", 3, 1, 6);
            Exception lastException = null;
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    return await operation().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    lastException = exception;
                    if (attempt == attempts) break;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))).ConfigureAwait(false);
                }
            }

            throw lastException ?? new InvalidOperationException("YouTube synchronization failed.");
        }

        private static void Consolidate(AnalyticsSyncBatch batch)
        {
            batch.Videos = batch.Videos
                .GroupBy(video => video.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();

            batch.ChannelDaily = batch.ChannelDaily
                .GroupBy(metric => new
                {
                    Date = metric.Date.Date,
                    Country = metric.CountryCode ?? string.Empty,
                    Content = metric.ContentType ?? "Unspecified"
                })
                .Select(group => new DailyChannelAnalyticsMetric
                {
                    Date = group.Key.Date,
                    CountryCode = group.Key.Country,
                    ContentType = group.Key.Content,
                    Metrics = Aggregate(group.Select(metric => metric.Metrics))
                })
                .ToList();

            batch.VideoDaily = batch.VideoDaily
                .GroupBy(metric => new
                {
                    Date = metric.Date.Date,
                    Country = metric.CountryCode ?? string.Empty,
                    metric.Metrics.VideoId
                })
                .Select(group => new DailyVideoAnalyticsMetric
                {
                    Date = group.Key.Date,
                    CountryCode = group.Key.Country,
                    Metrics = AggregateVideos(group.Select(metric => metric.Metrics), group.Key.VideoId)
                })
                .ToList();

            batch.TrafficSourceDaily = batch.TrafficSourceDaily
                .GroupBy(metric => new
                {
                    Date = metric.Date.Date,
                    Country = metric.CountryCode ?? string.Empty,
                    Content = metric.ContentType ?? "Unspecified",
                    Source = metric.TrafficSource ?? "UNKNOWN"
                })
                .Select(group =>
                {
                    long views = group.Sum(metric => metric.Views);
                    return new DailyTrafficSourceAnalyticsMetric
                    {
                        Date = group.Key.Date,
                        CountryCode = group.Key.Country,
                        ContentType = group.Key.Content,
                        TrafficSource = group.Key.Source,
                        Views = views,
                        WatchTimeMinutes = group.Sum(metric => metric.WatchTimeMinutes),
                        AverageViewDurationSeconds = WeightedAverage(
                            group.Select(metric => new WeightedValue(metric.Views, metric.AverageViewDurationSeconds))),
                        AverageViewPercentage = WeightedAverage(
                            group.Select(metric => new WeightedValue(metric.Views, metric.AverageViewPercentage)))
                    };
                })
                .ToList();
        }

        private static AnalyticsSummary Aggregate(IEnumerable<AnalyticsSummary> source)
        {
            IList<AnalyticsSummary> metrics = source.ToList();
            long views = metrics.Sum(metric => metric.Views);
            return new AnalyticsSummary
            {
                Views = views,
                WatchTimeMinutes = metrics.Sum(metric => metric.WatchTimeMinutes),
                AverageViewDurationSeconds = WeightedAverage(metrics.Select(metric =>
                    new WeightedValue(metric.Views, metric.AverageViewDurationSeconds))),
                AverageViewPercentage = WeightedAverage(metrics.Select(metric =>
                    new WeightedValue(metric.Views, metric.AverageViewPercentage))),
                Likes = metrics.Sum(metric => metric.Likes),
                Comments = metrics.Sum(metric => metric.Comments),
                Shares = metrics.Sum(metric => metric.Shares),
                SubscribersGained = metrics.Sum(metric => metric.SubscribersGained),
                SubscribersLost = metrics.Sum(metric => metric.SubscribersLost)
            };
        }

        private static VideoAnalyticsMetric AggregateVideos(
            IEnumerable<VideoAnalyticsMetric> source,
            string videoId)
        {
            IList<VideoAnalyticsMetric> metrics = source.ToList();
            return new VideoAnalyticsMetric
            {
                VideoId = videoId,
                Views = metrics.Sum(metric => metric.Views),
                WatchTimeMinutes = metrics.Sum(metric => metric.WatchTimeMinutes),
                AverageViewDurationSeconds = WeightedAverage(metrics.Select(metric =>
                    new WeightedValue(metric.Views, metric.AverageViewDurationSeconds))),
                AverageViewPercentage = WeightedAverage(metrics.Select(metric =>
                    new WeightedValue(metric.Views, metric.AverageViewPercentage))),
                Likes = metrics.Sum(metric => metric.Likes),
                Comments = metrics.Sum(metric => metric.Comments),
                Shares = metrics.Sum(metric => metric.Shares),
                SubscribersGained = metrics.Sum(metric => metric.SubscribersGained),
                SubscribersLost = metrics.Sum(metric => metric.SubscribersLost)
            };
        }

        private static double WeightedAverage(IEnumerable<WeightedValue> source)
        {
            IList<WeightedValue> values = source.ToList();
            long weight = values.Sum(value => value.Weight);
            return weight == 0
                ? 0d
                : values.Sum(value => value.Value * value.Weight) / weight;
        }

        private sealed class WeightedValue
        {
            public WeightedValue(long weight, double value)
            {
                Weight = weight;
                Value = value;
            }

            public long Weight { get; private set; }

            public double Value { get; private set; }
        }

        private static int ReadInt(string key, int defaultValue, int minimum, int maximum)
        {
            int value;
            return int.TryParse(ConfigurationManager.AppSettings[key], out value)
                ? Math.Max(minimum, Math.Min(maximum, value))
                : defaultValue;
        }

        private static DateTime Max(DateTime left, DateTime right)
        {
            return left > right ? left : right;
        }

        private static IList<AnalyticsPeriod> MergePeriods(IEnumerable<AnalyticsPeriod> periods)
        {
            var merged = new List<AnalyticsPeriod>();
            foreach (AnalyticsPeriod period in periods.OrderBy(item => item.StartDate))
            {
                AnalyticsPeriod current = merged.LastOrDefault();
                if (current == null || period.StartDate.Date > current.EndDate.Date.AddDays(1))
                {
                    merged.Add(new AnalyticsPeriod
                    {
                        StartDate = period.StartDate.Date,
                        EndDate = period.EndDate.Date
                    });
                    continue;
                }

                if (period.EndDate.Date > current.EndDate)
                {
                    current.EndDate = period.EndDate.Date;
                }
            }

            return merged;
        }
    }
}
