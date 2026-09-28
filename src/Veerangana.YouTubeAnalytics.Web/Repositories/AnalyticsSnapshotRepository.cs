using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Infrastructure.Data;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Repositories
{
    public sealed class AnalyticsSnapshotRepository : IAnalyticsSnapshotRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public AnalyticsSnapshotRepository(IDbConnectionFactory connectionFactory)
        {
            if (connectionFactory == null) throw new ArgumentNullException("connectionFactory");
            _connectionFactory = connectionFactory;
        }

        public async Task SaveOverviewSnapshotAsync(
            YouTubeChannelInfo channel,
            IEnumerable<YouTubeVideoInfo> videos,
            DateTime snapshotDate)
        {
            if (channel == null) throw new ArgumentNullException("channel");

            IList<YouTubeVideoInfo> videoList = (videos ?? Enumerable.Empty<YouTubeVideoInfo>()).ToList();
            using (DbConnection connection = _connectionFactory.CreateConnection())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                using (DbTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        await SaveChannelAsync(connection, transaction, channel, snapshotDate.Date)
                            .ConfigureAwait(false);

                        foreach (YouTubeVideoInfo video in videoList)
                        {
                            await SaveVideoAsync(connection, transaction, video, snapshotDate.Date)
                                .ConfigureAwait(false);
                        }

                        await SaveSyncLogAsync(connection, transaction, videoList.Count)
                            .ConfigureAwait(false);
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

        private static async Task SaveChannelAsync(
            DbConnection connection,
            DbTransaction transaction,
            YouTubeChannelInfo channel,
            DateTime snapshotDate)
        {
            using (DbCommand command = CreateCommand(connection, transaction, @"
UPDATE dbo.ChannelSnapshot
SET Views = @Views, Subscribers = @Subscribers, VideoCount = @VideoCount
WHERE ChannelId = @ChannelId AND SnapshotDate = @SnapshotDate;

IF @@ROWCOUNT = 0
BEGIN
    INSERT dbo.ChannelSnapshot (ChannelId, SnapshotDate, Views, Subscribers, VideoCount)
    VALUES (@ChannelId, @SnapshotDate, @Views, @Subscribers, @VideoCount);
END;"))
            {
                AddParameter(command, "@ChannelId", DbType.String, channel.Id);
                AddParameter(command, "@SnapshotDate", DbType.Date, snapshotDate);
                AddParameter(command, "@Views", DbType.Int64, channel.ViewCount);
                AddParameter(command, "@Subscribers", DbType.Int64, channel.SubscriberCount);
                AddParameter(command, "@VideoCount", DbType.Int32, channel.VideoCount);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        private static async Task SaveVideoAsync(
            DbConnection connection,
            DbTransaction transaction,
            YouTubeVideoInfo video,
            DateTime snapshotDate)
        {
            using (DbCommand command = CreateCommand(connection, transaction, @"
UPDATE dbo.VideoMetadata
SET ChannelId = @ChannelId, Title = @Title, ThumbnailUrl = @ThumbnailUrl,
    PublishedAtUtc = @PublishedAtUtc, DurationSeconds = @DurationSeconds,
    ContentType = @ContentType, IsActive = 1, LastSyncedAtUtc = SYSUTCDATETIME(),
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE VideoId = @VideoId;

IF @@ROWCOUNT = 0
BEGIN
    INSERT dbo.VideoMetadata
        (VideoId, ChannelId, Title, ThumbnailUrl, PublishedAtUtc, DurationSeconds,
         ContentType, IsActive, LastSyncedAtUtc)
    VALUES
        (@VideoId, @ChannelId, @Title, @ThumbnailUrl, @PublishedAtUtc, @DurationSeconds,
         @ContentType, 1, SYSUTCDATETIME());
END;

UPDATE dbo.VideoSnapshot
SET Views = @Views, Likes = @Likes, Comments = @Comments
WHERE VideoId = @VideoId AND SnapshotDate = @SnapshotDate;

IF @@ROWCOUNT = 0
BEGIN
    INSERT dbo.VideoSnapshot (VideoId, SnapshotDate, Views, Likes, Comments)
    VALUES (@VideoId, @SnapshotDate, @Views, @Likes, @Comments);
END;"))
            {
                AddParameter(command, "@VideoId", DbType.String, video.Id);
                AddParameter(command, "@ChannelId", DbType.String, video.ChannelId);
                AddParameter(command, "@Title", DbType.String, video.Title);
                AddParameter(command, "@ThumbnailUrl", DbType.String, video.ThumbnailUrl);
                AddParameter(command, "@PublishedAtUtc", DbType.DateTime2, video.PublishedAtUtc);
                AddParameter(command, "@DurationSeconds", DbType.Int32, video.DurationSeconds);
                AddParameter(
                    command,
                    "@ContentType",
                    DbType.String,
                    video.IsLiveStream ? "Live" : video.DurationSeconds <= 180 ? "Short" : "Video");
                AddParameter(command, "@SnapshotDate", DbType.Date, snapshotDate);
                AddParameter(command, "@Views", DbType.Int64, video.ViewCount);
                AddParameter(command, "@Likes", DbType.Int64, video.LikeCount);
                AddParameter(command, "@Comments", DbType.Int64, video.CommentCount);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        private static async Task SaveSyncLogAsync(
            DbConnection connection,
            DbTransaction transaction,
            int recordsProcessed)
        {
            using (DbCommand command = CreateCommand(connection, transaction, @"
INSERT dbo.AnalyticsSyncLog
    (SyncType, Status, StartedAtUtc, CompletedAtUtc, RecordsProcessed)
VALUES
    ('Overview', 'Completed', SYSUTCDATETIME(), SYSUTCDATETIME(), @RecordsProcessed);"))
            {
                AddParameter(command, "@RecordsProcessed", DbType.Int32, recordsProcessed);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        private static DbCommand CreateCommand(
            DbConnection connection,
            DbTransaction transaction,
            string commandText)
        {
            DbCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = commandText;
            command.CommandTimeout = 20;
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
    }
}
