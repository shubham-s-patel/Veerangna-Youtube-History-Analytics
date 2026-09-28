using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Infrastructure.Data;
using Veerangana.YouTubeAnalytics.Models;
using Veerangana.YouTubeAnalytics.ViewModels;

namespace Veerangana.YouTubeAnalytics.Repositories
{
    public sealed class AnalyticsFactRepository : IAnalyticsFactRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public AnalyticsFactRepository(IDbConnectionFactory connectionFactory)
        {
            if (connectionFactory == null) throw new ArgumentNullException("connectionFactory");
            _connectionFactory = connectionFactory;
        }

        public async Task<DashboardOverviewData> GetOverviewAsync(GlobalFilterViewModel filters)
        {
            AnalyticsReportData report = await GetReportAsync(
                AnalyticsReportKind.Reports,
                filters).ConfigureAwait(false);

            return new DashboardOverviewData
            {
                Channel = report.Channel,
                Period = report.Period,
                Current = report.Current,
                Previous = report.Previous,
                Trend = report.Trend,
                TopVideos = report.TopVideos.Take(10).ToList()
            };
        }

        public async Task<AnalyticsReportData> GetReportAsync(
            AnalyticsReportKind reportKind,
            GlobalFilterViewModel filters)
        {
            if (filters == null) throw new ArgumentNullException("filters");

            using (DbConnection connection = _connectionFactory.CreateConnection())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                YouTubeChannelInfo channel = await LoadChannelAsync(connection).ConfigureAwait(false);
                AnalyticsPeriod factCoverage = await LoadFactCoverageAsync(
                    connection,
                    channel.Id).ConfigureAwait(false);
                AnalyticsPeriod period = BuildPeriod(
                    filters,
                    channel.PublishedAtUtc,
                    factCoverage);
                AnalyticsPeriod previousPeriod = PreviousPeriod(period);
                string contentType = MapContentType(filters.ContentType);
                string countryCode = filters.Country ?? string.Empty;

                AnalyticsSummary current = await LoadSummaryAsync(
                    connection,
                    channel.Id,
                    period,
                    countryCode,
                    contentType).ConfigureAwait(false);
                AnalyticsSummary previous = filters.ComparePeriod &&
                    filters.DateRangeKey != "lifetime" &&
                    previousPeriod.StartDate >= factCoverage.StartDate
                    ? await LoadSummaryAsync(
                        connection,
                        channel.Id,
                        previousPeriod,
                        countryCode,
                        contentType).ConfigureAwait(false)
                    : null;
                IList<AnalyticsTrendPoint> trend = await LoadTrendAsync(
                    connection,
                    channel.Id,
                    period,
                    countryCode,
                    contentType).ConfigureAwait(false);

                IList<VideoUsageRecord> videos = RequiresVideos(reportKind)
                    ? await LoadVideosAsync(
                        connection,
                        period,
                        countryCode,
                        contentType,
                        reportKind == AnalyticsReportKind.Videos ? 200 : 15).ConfigureAwait(false)
                    : new List<VideoUsageRecord>();

                IList<AnalyticsBreakdownMetric> breakdown;
                if (reportKind == AnalyticsReportKind.TrafficSources)
                {
                    breakdown = await LoadTrafficSourcesAsync(
                        connection,
                        channel.Id,
                        period,
                        countryCode,
                        contentType).ConfigureAwait(false);
                }
                else if (reportKind == AnalyticsReportKind.Geography)
                {
                    breakdown = await LoadCountriesAsync(
                        connection,
                        channel.Id,
                        period,
                        countryCode,
                        contentType).ConfigureAwait(false);
                }
                else
                {
                    breakdown = new List<AnalyticsBreakdownMetric>();
                }

                return new AnalyticsReportData
                {
                    Channel = channel,
                    Period = period,
                    Current = current,
                    Previous = previous,
                    Trend = trend,
                    TopVideos = videos,
                    Breakdown = breakdown
                };
            }
        }

        public async Task<AnalyticsSyncLease> TryBeginSyncAsync(
            string trigger,
            TimeSpan leaseDuration)
        {
            using (DbConnection connection = _connectionFactory.CreateConnection())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                using (DbTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                using (DbCommand stateCommand = CreateCommand(connection, transaction, @"
UPDATE dbo.AnalyticsSyncState WITH (UPDLOCK, HOLDLOCK)
SET IsRunning = 1,
    LeaseExpiresAtUtc = DATEADD(MINUTE, @LeaseMinutes, SYSUTCDATETIME()),
    LastStartedAtUtc = SYSUTCDATETIME(),
    LastErrorMessage = NULL
OUTPUT deleted.OldestSyncedDate, deleted.LatestSyncedDate
WHERE Id = 1
  AND (IsRunning = 0 OR LeaseExpiresAtUtc < SYSUTCDATETIME());"))
                {
                    AddParameter(stateCommand, "@LeaseMinutes", DbType.Int32,
                        Math.Max(5, (int)Math.Ceiling(leaseDuration.TotalMinutes)));
                    DateTime? oldest;
                    DateTime? latest;
                    DateTime? oldestCountryVideo;
                    using (DbDataReader reader = await stateCommand.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        if (!await reader.ReadAsync().ConfigureAwait(false))
                        {
                            transaction.Rollback();
                            return null;
                        }

                        oldest = reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0);
                        latest = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
                    }

                    using (DbCommand countryVideoCommand = CreateCommand(connection, transaction, @"
SELECT MIN(MetricDate)
FROM dbo.VideoAnalyticsDaily
WHERE CountryCode <> '';"))
                    {
                        object value = await countryVideoCommand.ExecuteScalarAsync().ConfigureAwait(false);
                        oldestCountryVideo = value == null || value == DBNull.Value
                            ? (DateTime?)null
                            : Convert.ToDateTime(value).Date;
                    }

                    long logId;
                    using (DbCommand logCommand = CreateCommand(connection, transaction, @"
INSERT dbo.AnalyticsSyncLog (SyncType, Status, StartedAtUtc, RecordsProcessed)
VALUES (@SyncType, 'Started', SYSUTCDATETIME(), 0);
SELECT CAST(SCOPE_IDENTITY() AS bigint);"))
                    {
                        AddParameter(logCommand, "@SyncType", DbType.String,
                            "YouTube-" + (string.IsNullOrWhiteSpace(trigger) ? "Scheduled" : trigger));
                        logId = Convert.ToInt64(
                            await logCommand.ExecuteScalarAsync().ConfigureAwait(false));
                    }

                    transaction.Commit();
                    return new AnalyticsSyncLease
                    {
                        LogId = logId,
                        OldestSyncedDate = oldest,
                        LatestSyncedDate = latest,
                        OldestCountryVideoDate = oldestCountryVideo
                    };
                }
            }
        }

        public async Task SaveSyncBatchAsync(AnalyticsSyncBatch batch)
        {
            if (batch == null) throw new ArgumentNullException("batch");
            if (batch.Channel == null) throw new ArgumentException("Channel is required.", "batch");

            using (DbConnection connection = _connectionFactory.CreateConnection())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                using (DbTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        await ReplaceRangesAsync(connection, transaction, batch.Periods).ConfigureAwait(false);
                        await SaveChannelAsync(connection, transaction, batch.Channel).ConfigureAwait(false);

                        foreach (YouTubeVideoInfo video in batch.Videos)
                        {
                            await SaveVideoMetadataAsync(connection, transaction, video).ConfigureAwait(false);
                        }

                        foreach (DailyChannelAnalyticsMetric metric in batch.ChannelDaily)
                        {
                            await SaveChannelMetricAsync(
                                connection,
                                transaction,
                                batch.Channel.Id,
                                metric).ConfigureAwait(false);
                        }

                        foreach (DailyVideoAnalyticsMetric metric in batch.VideoDaily)
                        {
                            await SaveVideoMetricAsync(connection, transaction, metric).ConfigureAwait(false);
                        }

                        foreach (DailyTrafficSourceAnalyticsMetric metric in batch.TrafficSourceDaily)
                        {
                            await SaveTrafficSourceMetricAsync(
                                connection,
                                transaction,
                                batch.Channel.Id,
                                metric).ConfigureAwait(false);
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task CompleteSyncAsync(
            AnalyticsSyncLease lease,
            int recordsProcessed,
            DateTime oldestSyncedDate,
            DateTime latestSyncedDate)
        {
            if (lease == null) throw new ArgumentNullException("lease");

            using (DbConnection connection = _connectionFactory.CreateConnection())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                using (DbTransaction transaction = connection.BeginTransaction())
                {
                    using (DbCommand stateCommand = CreateCommand(connection, transaction, @"
UPDATE dbo.AnalyticsSyncState
SET IsRunning = 0,
    LeaseExpiresAtUtc = NULL,
    LastCompletedAtUtc = SYSUTCDATETIME(),
    OldestSyncedDate = CASE
        WHEN OldestSyncedDate IS NULL OR OldestSyncedDate > @OldestDate THEN @OldestDate
        ELSE OldestSyncedDate END,
    LatestSyncedDate = CASE
        WHEN LatestSyncedDate IS NULL OR LatestSyncedDate < @LatestDate THEN @LatestDate
        ELSE LatestSyncedDate END,
    LastErrorMessage = NULL
WHERE Id = 1;"))
                    {
                        AddParameter(stateCommand, "@OldestDate", DbType.Date, oldestSyncedDate.Date);
                        AddParameter(stateCommand, "@LatestDate", DbType.Date, latestSyncedDate.Date);
                        await stateCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }

                    using (DbCommand logCommand = CreateCommand(connection, transaction, @"
UPDATE dbo.AnalyticsSyncLog
SET Status = 'Completed', CompletedAtUtc = SYSUTCDATETIME(), RecordsProcessed = @Records
WHERE Id = @LogId;"))
                    {
                        AddParameter(logCommand, "@Records", DbType.Int32, recordsProcessed);
                        AddParameter(logCommand, "@LogId", DbType.Int64, lease.LogId);
                        await logCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }

                    transaction.Commit();
                }
            }
        }

        public async Task FailSyncAsync(AnalyticsSyncLease lease, Exception exception)
        {
            if (lease == null) return;
            string message = exception == null ? "Unknown synchronization error." : exception.Message;
            if (message.Length > 1000) message = message.Substring(0, 1000);

            using (DbConnection connection = _connectionFactory.CreateConnection())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                using (DbTransaction transaction = connection.BeginTransaction())
                {
                    using (DbCommand stateCommand = CreateCommand(connection, transaction, @"
UPDATE dbo.AnalyticsSyncState
SET IsRunning = 0, LeaseExpiresAtUtc = NULL, LastErrorMessage = @Message
WHERE Id = 1;"))
                    {
                        AddParameter(stateCommand, "@Message", DbType.String, message);
                        await stateCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }

                    using (DbCommand logCommand = CreateCommand(connection, transaction, @"
UPDATE dbo.AnalyticsSyncLog
SET Status = 'Failed', CompletedAtUtc = SYSUTCDATETIME(), ErrorMessage = @Message
WHERE Id = @LogId;"))
                    {
                        AddParameter(logCommand, "@Message", DbType.String, message);
                        AddParameter(logCommand, "@LogId", DbType.Int64, lease.LogId);
                        await logCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }

                    transaction.Commit();
                }
            }
        }

        private static async Task<YouTubeChannelInfo> LoadChannelAsync(DbConnection connection)
        {
            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT TOP (1)
    metadata.ChannelId,
    metadata.Title,
    metadata.AvatarUrl,
    metadata.PublishedAtUtc,
    metadata.LastSyncedAtUtc,
    snapshot.Views,
    snapshot.Subscribers,
    snapshot.VideoCount
FROM dbo.ChannelMetadata metadata
OUTER APPLY
(
    SELECT TOP (1) Views, Subscribers, VideoCount
    FROM dbo.ChannelSnapshot
    WHERE ChannelId = metadata.ChannelId
    ORDER BY SnapshotDate DESC
) snapshot
ORDER BY metadata.LastSyncedAtUtc DESC;";
                using (DbDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    if (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        return new YouTubeChannelInfo
                        {
                            Id = reader.GetString(0),
                            Title = reader.GetString(1),
                            AvatarUrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            PublishedAtUtc = reader.IsDBNull(3) ? DateTime.UtcNow.Date.AddYears(-1) : reader.GetDateTime(3),
                            LastSyncedAtUtc = reader.GetDateTime(4),
                            ViewCount = reader.IsDBNull(5) ? 0L : reader.GetInt64(5),
                            SubscriberCount = reader.IsDBNull(6) ? (long?)null : reader.GetInt64(6),
                            VideoCount = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
                        };
                    }
                }
            }

            using (DbCommand fallback = connection.CreateCommand())
            {
                fallback.CommandText = @"
SELECT TOP (1)
    snapshot.ChannelId,
    snapshot.Views,
    snapshot.Subscribers,
    snapshot.VideoCount,
    (SELECT MIN(PublishedAtUtc) FROM dbo.VideoMetadata WHERE ChannelId = snapshot.ChannelId)
FROM dbo.ChannelSnapshot snapshot
ORDER BY snapshot.SnapshotDate DESC;";
                using (DbDataReader reader = await fallback.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync().ConfigureAwait(false))
                    {
                        throw new InvalidOperationException("The analytics database has not been synchronized yet.");
                    }

                    return new YouTubeChannelInfo
                    {
                        Id = reader.GetString(0),
                        Title = "Veerangana library",
                        PublishedAtUtc = reader.IsDBNull(4) ? DateTime.UtcNow.Date.AddYears(-1) : reader.GetDateTime(4),
                        LastSyncedAtUtc = DateTime.UtcNow,
                        ViewCount = reader.GetInt64(1),
                        SubscriberCount = reader.IsDBNull(2) ? (long?)null : reader.GetInt64(2),
                        VideoCount = reader.GetInt32(3)
                    };
                }
            }
        }

        private static async Task<AnalyticsPeriod> LoadFactCoverageAsync(
            DbConnection connection,
            string channelId)
        {
            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT MIN(MetricDate), MAX(MetricDate)
FROM dbo.ChannelAnalyticsDaily
WHERE ChannelId = @ChannelId AND CountryCode = '';";
                AddParameter(command, "@ChannelId", DbType.String, channelId);
                using (DbDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    await reader.ReadAsync().ConfigureAwait(false);
                    if (reader.IsDBNull(0) || reader.IsDBNull(1))
                    {
                        throw new InvalidOperationException("The analytics database has not been synchronized yet.");
                    }

                    return new AnalyticsPeriod
                    {
                        StartDate = reader.GetDateTime(0).Date,
                        EndDate = reader.GetDateTime(1).Date
                    };
                }
            }
        }

        private static async Task<AnalyticsSummary> LoadSummaryAsync(
            DbConnection connection,
            string channelId,
            AnalyticsPeriod period,
            string countryCode,
            string contentType)
        {
            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    COALESCE(SUM(Views), 0),
    COALESCE(SUM(WatchTimeMinutes), 0),
    COALESCE(SUM(CAST(AverageViewDurationSeconds AS decimal(38, 6)) * Views) /
        NULLIF(SUM(Views), 0), 0),
    COALESCE(SUM(CAST(AverageViewPercentage AS decimal(38, 6)) * Views) /
        NULLIF(SUM(Views), 0), 0),
    COALESCE(SUM(Likes), 0),
    COALESCE(SUM(Comments), 0),
    COALESCE(SUM(Shares), 0),
    COALESCE(SUM(SubscribersGained), 0),
    COALESCE(SUM(SubscribersLost), 0)
FROM dbo.ChannelAnalyticsDaily
WHERE ChannelId = @ChannelId
  AND MetricDate BETWEEN @StartDate AND @EndDate
  AND CountryCode = @CountryCode
  AND (@ContentType = '' OR ContentType = @ContentType);";
                AddPeriodParameters(command, channelId, period, countryCode, contentType);
                using (DbDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    await reader.ReadAsync().ConfigureAwait(false);
                    return ReadSummary(reader);
                }
            }
        }

        private static async Task<IList<AnalyticsTrendPoint>> LoadTrendAsync(
            DbConnection connection,
            string channelId,
            AnalyticsPeriod period,
            string countryCode,
            string contentType)
        {
            var result = new List<AnalyticsTrendPoint>();
            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT MetricDate, SUM(Views), SUM(WatchTimeMinutes),
       SUM(SubscribersGained), SUM(SubscribersLost)
FROM dbo.ChannelAnalyticsDaily
WHERE ChannelId = @ChannelId
  AND MetricDate BETWEEN @StartDate AND @EndDate
  AND CountryCode = @CountryCode
  AND (@ContentType = '' OR ContentType = @ContentType)
GROUP BY MetricDate
ORDER BY MetricDate;";
                AddPeriodParameters(command, channelId, period, countryCode, contentType);
                using (DbDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        result.Add(new AnalyticsTrendPoint
                        {
                            Date = reader.GetDateTime(0),
                            Views = Convert.ToInt64(reader.GetValue(1)),
                            WatchTimeMinutes = Convert.ToDouble(reader.GetValue(2)),
                            SubscribersGained = Convert.ToInt64(reader.GetValue(3)),
                            SubscribersLost = Convert.ToInt64(reader.GetValue(4))
                        });
                    }
                }
            }

            return result;
        }

        private static async Task<IList<VideoUsageRecord>> LoadVideosAsync(
            DbConnection connection,
            AnalyticsPeriod period,
            string countryCode,
            string contentType,
            int limit)
        {
            var result = new List<VideoUsageRecord>();
            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT TOP (@Limit)
    metadata.VideoId, metadata.ChannelId, metadata.Title, metadata.ThumbnailUrl,
    metadata.PublishedAtUtc, metadata.DurationSeconds, metadata.ContentType,
    SUM(fact.Views), SUM(fact.WatchTimeMinutes),
    COALESCE(SUM(CAST(fact.AverageViewDurationSeconds AS decimal(38, 6)) * fact.Views) /
        NULLIF(SUM(fact.Views), 0), 0),
    COALESCE(SUM(CAST(fact.AverageViewPercentage AS decimal(38, 6)) * fact.Views) /
        NULLIF(SUM(fact.Views), 0), 0),
    SUM(fact.Likes), SUM(fact.Comments), SUM(fact.Shares),
    SUM(fact.SubscribersGained), SUM(fact.SubscribersLost)
FROM dbo.VideoAnalyticsDaily fact
INNER JOIN dbo.VideoMetadata metadata ON metadata.VideoId = fact.VideoId
WHERE fact.MetricDate BETWEEN @StartDate AND @EndDate
  AND fact.CountryCode = @CountryCode
  AND (@ContentType = '' OR metadata.ContentType = @ContentType)
GROUP BY metadata.VideoId, metadata.ChannelId, metadata.Title, metadata.ThumbnailUrl,
         metadata.PublishedAtUtc, metadata.DurationSeconds, metadata.ContentType
ORDER BY SUM(fact.Views) DESC;";
                AddParameter(command, "@Limit", DbType.Int32, limit);
                AddParameter(command, "@StartDate", DbType.Date, period.StartDate.Date);
                AddParameter(command, "@EndDate", DbType.Date, period.EndDate.Date);
                AddParameter(command, "@CountryCode", DbType.String, countryCode ?? string.Empty);
                AddParameter(command, "@ContentType", DbType.String, contentType);
                using (DbDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        result.Add(new VideoUsageRecord
                        {
                            Video = new YouTubeVideoInfo
                            {
                                Id = reader.GetString(0),
                                ChannelId = reader.GetString(1),
                                Title = reader.GetString(2),
                                ThumbnailUrl = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                PublishedAtUtc = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
                                DurationSeconds = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                IsLiveStream = string.Equals(reader.GetString(6), "Live", StringComparison.OrdinalIgnoreCase)
                            },
                            Analytics = new VideoAnalyticsMetric
                            {
                                VideoId = reader.GetString(0),
                                Views = Convert.ToInt64(reader.GetValue(7)),
                                WatchTimeMinutes = Convert.ToDouble(reader.GetValue(8)),
                                AverageViewDurationSeconds = Convert.ToDouble(reader.GetValue(9)),
                                AverageViewPercentage = Convert.ToDouble(reader.GetValue(10)),
                                Likes = Convert.ToInt64(reader.GetValue(11)),
                                Comments = Convert.ToInt64(reader.GetValue(12)),
                                Shares = Convert.ToInt64(reader.GetValue(13)),
                                SubscribersGained = Convert.ToInt64(reader.GetValue(14)),
                                SubscribersLost = Convert.ToInt64(reader.GetValue(15))
                            }
                        });
                    }
                }
            }

            return result;
        }

        private static async Task<IList<AnalyticsBreakdownMetric>> LoadTrafficSourcesAsync(
            DbConnection connection,
            string channelId,
            AnalyticsPeriod period,
            string countryCode,
            string contentType)
        {
            return await LoadBreakdownAsync(connection, @"
SELECT TrafficSource, SUM(Views), SUM(WatchTimeMinutes),
       COALESCE(SUM(CAST(AverageViewDurationSeconds AS decimal(38, 6)) * Views) /
           NULLIF(SUM(Views), 0), 0),
       COALESCE(SUM(CAST(AverageViewPercentage AS decimal(38, 6)) * Views) /
           NULLIF(SUM(Views), 0), 0)
FROM dbo.TrafficSourceAnalyticsDaily
WHERE ChannelId = @ChannelId
  AND MetricDate BETWEEN @StartDate AND @EndDate
  AND CountryCode = @CountryCode
  AND (@ContentType = '' OR ContentType = @ContentType)
GROUP BY TrafficSource
ORDER BY SUM(Views) DESC;", channelId, period, countryCode, contentType).ConfigureAwait(false);
        }

        private static async Task<IList<AnalyticsBreakdownMetric>> LoadCountriesAsync(
            DbConnection connection,
            string channelId,
            AnalyticsPeriod period,
            string countryCode,
            string contentType)
        {
            return await LoadBreakdownAsync(connection, @"
SELECT CountryCode, SUM(Views), SUM(WatchTimeMinutes),
       COALESCE(SUM(CAST(AverageViewDurationSeconds AS decimal(38, 6)) * Views) /
           NULLIF(SUM(Views), 0), 0),
       COALESCE(SUM(CAST(AverageViewPercentage AS decimal(38, 6)) * Views) /
           NULLIF(SUM(Views), 0), 0)
FROM dbo.ChannelAnalyticsDaily
WHERE ChannelId = @ChannelId
  AND MetricDate BETWEEN @StartDate AND @EndDate
  AND CountryCode <> ''
  AND (@CountryCode = '' OR CountryCode = @CountryCode)
  AND (@ContentType = '' OR ContentType = @ContentType)
GROUP BY CountryCode
ORDER BY SUM(Views) DESC;", channelId, period, countryCode, contentType).ConfigureAwait(false);
        }

        private static async Task<IList<AnalyticsBreakdownMetric>> LoadBreakdownAsync(
            DbConnection connection,
            string sql,
            string channelId,
            AnalyticsPeriod period,
            string countryCode,
            string contentType)
        {
            var result = new List<AnalyticsBreakdownMetric>();
            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddPeriodParameters(command, channelId, period, countryCode, contentType);
                using (DbDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        result.Add(new AnalyticsBreakdownMetric
                        {
                            Key = reader.GetString(0),
                            Views = Convert.ToInt64(reader.GetValue(1)),
                            WatchTimeMinutes = Convert.ToDouble(reader.GetValue(2)),
                            AverageViewDurationSeconds = Convert.ToDouble(reader.GetValue(3)),
                            AverageViewPercentage = Convert.ToDouble(reader.GetValue(4))
                        });
                    }
                }
            }

            return result;
        }

        private static async Task ReplaceRangesAsync(
            DbConnection connection,
            DbTransaction transaction,
            IEnumerable<AnalyticsPeriod> periods)
        {
            foreach (AnalyticsPeriod period in periods)
            {
                using (DbCommand command = CreateCommand(connection, transaction, @"
DELETE FROM dbo.VideoAnalyticsDaily WHERE MetricDate BETWEEN @StartDate AND @EndDate;
DELETE FROM dbo.TrafficSourceAnalyticsDaily WHERE MetricDate BETWEEN @StartDate AND @EndDate;
DELETE FROM dbo.ChannelAnalyticsDaily WHERE MetricDate BETWEEN @StartDate AND @EndDate;"))
                {
                    AddParameter(command, "@StartDate", DbType.Date, period.StartDate.Date);
                    AddParameter(command, "@EndDate", DbType.Date, period.EndDate.Date);
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        private static async Task SaveChannelAsync(
            DbConnection connection,
            DbTransaction transaction,
            YouTubeChannelInfo channel)
        {
            using (DbCommand command = CreateCommand(connection, transaction, @"
UPDATE dbo.ChannelMetadata
SET Title = @Title, AvatarUrl = @AvatarUrl, PublishedAtUtc = @PublishedAtUtc,
    LastSyncedAtUtc = SYSUTCDATETIME()
WHERE ChannelId = @ChannelId;
IF @@ROWCOUNT = 0
    INSERT dbo.ChannelMetadata (ChannelId, Title, AvatarUrl, PublishedAtUtc, LastSyncedAtUtc)
    VALUES (@ChannelId, @Title, @AvatarUrl, @PublishedAtUtc, SYSUTCDATETIME());

UPDATE dbo.ChannelSnapshot
SET Views = @Views, Subscribers = @Subscribers, VideoCount = @VideoCount
WHERE ChannelId = @ChannelId AND SnapshotDate = CAST(SYSUTCDATETIME() AS date);
IF @@ROWCOUNT = 0
    INSERT dbo.ChannelSnapshot (ChannelId, SnapshotDate, Views, Subscribers, VideoCount)
    VALUES (@ChannelId, CAST(SYSUTCDATETIME() AS date), @Views, @Subscribers, @VideoCount);"))
            {
                AddParameter(command, "@ChannelId", DbType.String, channel.Id);
                AddParameter(command, "@Title", DbType.String, channel.Title);
                AddParameter(command, "@AvatarUrl", DbType.String, channel.AvatarUrl);
                AddParameter(command, "@PublishedAtUtc", DbType.DateTime2, channel.PublishedAtUtc);
                AddParameter(command, "@Views", DbType.Int64, channel.ViewCount);
                AddParameter(command, "@Subscribers", DbType.Int64, channel.SubscriberCount);
                AddParameter(command, "@VideoCount", DbType.Int32, channel.VideoCount);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        private static async Task SaveVideoMetadataAsync(
            DbConnection connection,
            DbTransaction transaction,
            YouTubeVideoInfo video)
        {
            using (DbCommand command = CreateCommand(connection, transaction, @"
UPDATE dbo.VideoMetadata
SET ChannelId = @ChannelId, Title = @Title, ThumbnailUrl = @ThumbnailUrl,
    PublishedAtUtc = @PublishedAtUtc, DurationSeconds = @DurationSeconds,
    ContentType = @ContentType, IsActive = 1, LastSyncedAtUtc = SYSUTCDATETIME(),
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE VideoId = @VideoId;
IF @@ROWCOUNT = 0
    INSERT dbo.VideoMetadata
        (VideoId, ChannelId, Title, ThumbnailUrl, PublishedAtUtc, DurationSeconds,
         ContentType, IsActive, LastSyncedAtUtc)
    VALUES
        (@VideoId, @ChannelId, @Title, @ThumbnailUrl, @PublishedAtUtc, @DurationSeconds,
         @ContentType, 1, SYSUTCDATETIME());

UPDATE dbo.VideoSnapshot
SET Views = @Views, Likes = @Likes, Comments = @Comments
WHERE VideoId = @VideoId AND SnapshotDate = CAST(SYSUTCDATETIME() AS date);
IF @@ROWCOUNT = 0
    INSERT dbo.VideoSnapshot (VideoId, SnapshotDate, Views, Likes, Comments)
    VALUES (@VideoId, CAST(SYSUTCDATETIME() AS date), @Views, @Likes, @Comments);"))
            {
                AddParameter(command, "@VideoId", DbType.String, video.Id);
                AddParameter(command, "@ChannelId", DbType.String, video.ChannelId);
                AddParameter(command, "@Title", DbType.String, video.Title);
                AddParameter(command, "@ThumbnailUrl", DbType.String, video.ThumbnailUrl);
                AddParameter(command, "@PublishedAtUtc", DbType.DateTime2, video.PublishedAtUtc);
                AddParameter(command, "@DurationSeconds", DbType.Int32, video.DurationSeconds);
                AddParameter(command, "@ContentType", DbType.String, ContentType(video));
                AddParameter(command, "@Views", DbType.Int64, video.ViewCount);
                AddParameter(command, "@Likes", DbType.Int64, video.LikeCount);
                AddParameter(command, "@Comments", DbType.Int64, video.CommentCount);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        private static async Task SaveChannelMetricAsync(
            DbConnection connection,
            DbTransaction transaction,
            string channelId,
            DailyChannelAnalyticsMetric metric)
        {
            using (DbCommand command = CreateCommand(connection, transaction, @"
INSERT dbo.ChannelAnalyticsDaily
    (ChannelId, MetricDate, CountryCode, ContentType, Views, WatchTimeMinutes,
     AverageViewDurationSeconds, AverageViewPercentage, Likes, Comments, Shares,
     SubscribersGained, SubscribersLost)
VALUES
    (@ChannelId, @MetricDate, @CountryCode, @ContentType, @Views, @WatchTime,
     @AverageDuration, @AveragePercentage, @Likes, @Comments, @Shares,
     @SubscribersGained, @SubscribersLost);"))
            {
                AddParameter(command, "@ChannelId", DbType.String, channelId);
                AddParameter(command, "@MetricDate", DbType.Date, metric.Date.Date);
                AddParameter(command, "@CountryCode", DbType.String, metric.CountryCode ?? string.Empty);
                AddParameter(command, "@ContentType", DbType.String, metric.ContentType);
                AddSummaryParameters(command, metric.Metrics);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        private static async Task SaveVideoMetricAsync(
            DbConnection connection,
            DbTransaction transaction,
            DailyVideoAnalyticsMetric metric)
        {
            using (DbCommand command = CreateCommand(connection, transaction, @"
INSERT dbo.VideoAnalyticsDaily
    (VideoId, MetricDate, CountryCode, Views, WatchTimeMinutes, AverageViewDurationSeconds,
     AverageViewPercentage, Likes, Comments, Shares, SubscribersGained, SubscribersLost)
VALUES
    (@VideoId, @MetricDate, @CountryCode, @Views, @WatchTime, @AverageDuration,
     @AveragePercentage, @Likes, @Comments, @Shares, @SubscribersGained, @SubscribersLost);"))
            {
                AddParameter(command, "@VideoId", DbType.String, metric.Metrics.VideoId);
                AddParameter(command, "@MetricDate", DbType.Date, metric.Date.Date);
                AddParameter(command, "@CountryCode", DbType.String, metric.CountryCode ?? string.Empty);
                AddParameter(command, "@Views", DbType.Int64, metric.Metrics.Views);
                AddParameter(command, "@WatchTime", DbType.Decimal, metric.Metrics.WatchTimeMinutes);
                AddParameter(command, "@AverageDuration", DbType.Decimal, metric.Metrics.AverageViewDurationSeconds);
                AddParameter(command, "@AveragePercentage", DbType.Decimal, metric.Metrics.AverageViewPercentage);
                AddParameter(command, "@Likes", DbType.Int64, metric.Metrics.Likes);
                AddParameter(command, "@Comments", DbType.Int64, metric.Metrics.Comments);
                AddParameter(command, "@Shares", DbType.Int64, metric.Metrics.Shares);
                AddParameter(command, "@SubscribersGained", DbType.Int64, metric.Metrics.SubscribersGained);
                AddParameter(command, "@SubscribersLost", DbType.Int64, metric.Metrics.SubscribersLost);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        private static async Task SaveTrafficSourceMetricAsync(
            DbConnection connection,
            DbTransaction transaction,
            string channelId,
            DailyTrafficSourceAnalyticsMetric metric)
        {
            using (DbCommand command = CreateCommand(connection, transaction, @"
INSERT dbo.TrafficSourceAnalyticsDaily
    (ChannelId, MetricDate, CountryCode, ContentType, TrafficSource, Views,
     WatchTimeMinutes, AverageViewDurationSeconds, AverageViewPercentage)
VALUES
    (@ChannelId, @MetricDate, @CountryCode, @ContentType, @TrafficSource, @Views,
     @WatchTime, @AverageDuration, @AveragePercentage);"))
            {
                AddParameter(command, "@ChannelId", DbType.String, channelId);
                AddParameter(command, "@MetricDate", DbType.Date, metric.Date.Date);
                AddParameter(command, "@CountryCode", DbType.String, metric.CountryCode ?? string.Empty);
                AddParameter(command, "@ContentType", DbType.String, metric.ContentType);
                AddParameter(command, "@TrafficSource", DbType.String, metric.TrafficSource);
                AddParameter(command, "@Views", DbType.Int64, metric.Views);
                AddParameter(command, "@WatchTime", DbType.Decimal, metric.WatchTimeMinutes);
                AddParameter(command, "@AverageDuration", DbType.Decimal, metric.AverageViewDurationSeconds);
                AddParameter(command, "@AveragePercentage", DbType.Decimal, metric.AverageViewPercentage);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        private static AnalyticsSummary ReadSummary(DbDataReader reader)
        {
            return new AnalyticsSummary
            {
                Views = Convert.ToInt64(reader.GetValue(0)),
                WatchTimeMinutes = Convert.ToDouble(reader.GetValue(1)),
                AverageViewDurationSeconds = Convert.ToDouble(reader.GetValue(2)),
                AverageViewPercentage = Convert.ToDouble(reader.GetValue(3)),
                Likes = Convert.ToInt64(reader.GetValue(4)),
                Comments = Convert.ToInt64(reader.GetValue(5)),
                Shares = Convert.ToInt64(reader.GetValue(6)),
                SubscribersGained = Convert.ToInt64(reader.GetValue(7)),
                SubscribersLost = Convert.ToInt64(reader.GetValue(8))
            };
        }

        private static void AddPeriodParameters(
            DbCommand command,
            string channelId,
            AnalyticsPeriod period,
            string countryCode,
            string contentType)
        {
            AddParameter(command, "@ChannelId", DbType.String, channelId);
            AddParameter(command, "@StartDate", DbType.Date, period.StartDate.Date);
            AddParameter(command, "@EndDate", DbType.Date, period.EndDate.Date);
            AddParameter(command, "@CountryCode", DbType.String, countryCode ?? string.Empty);
            AddParameter(command, "@ContentType", DbType.String, contentType ?? string.Empty);
        }

        private static void AddSummaryParameters(DbCommand command, AnalyticsSummary metrics)
        {
            AddParameter(command, "@Views", DbType.Int64, metrics.Views);
            AddParameter(command, "@WatchTime", DbType.Decimal, metrics.WatchTimeMinutes);
            AddParameter(command, "@AverageDuration", DbType.Decimal, metrics.AverageViewDurationSeconds);
            AddParameter(command, "@AveragePercentage", DbType.Decimal, metrics.AverageViewPercentage);
            AddParameter(command, "@Likes", DbType.Int64, metrics.Likes);
            AddParameter(command, "@Comments", DbType.Int64, metrics.Comments);
            AddParameter(command, "@Shares", DbType.Int64, metrics.Shares);
            AddParameter(command, "@SubscribersGained", DbType.Int64, metrics.SubscribersGained);
            AddParameter(command, "@SubscribersLost", DbType.Int64, metrics.SubscribersLost);
        }

        private static DbCommand CreateCommand(
            DbConnection connection,
            DbTransaction transaction,
            string commandText)
        {
            DbCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = commandText;
            command.CommandTimeout = 60;
            return command;
        }

        private static void AddParameter(DbCommand command, string name, DbType type, object value)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = type;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private static string ContentType(YouTubeVideoInfo video)
        {
            if (video.IsLiveStream) return "Live";
            return video.DurationSeconds <= 180 ? "Short" : "Video";
        }

        private static string MapContentType(string contentType)
        {
            switch ((contentType ?? string.Empty).ToLowerInvariant())
            {
                case "short": return "Short";
                case "live": return "Live";
                case "video": return "Video";
                default: return string.Empty;
            }
        }

        private static bool RequiresVideos(AnalyticsReportKind reportKind)
        {
            return reportKind != AnalyticsReportKind.TrafficSources &&
                   reportKind != AnalyticsReportKind.Geography;
        }

        private static AnalyticsPeriod BuildPeriod(
            GlobalFilterViewModel filters,
            DateTime channelPublishedAtUtc,
            AnalyticsPeriod factCoverage)
        {
            DateTime minimumStartDate = factCoverage.StartDate > channelPublishedAtUtc.Date
                ? factCoverage.StartDate
                : channelPublishedAtUtc.Date;
            DateTime maximumEndDate = factCoverage.EndDate;
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
            endDate = endDate < minimumStartDate ? minimumStartDate : endDate;
            startDate = startDate < minimumStartDate ? minimumStartDate : startDate;
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
    }
}
