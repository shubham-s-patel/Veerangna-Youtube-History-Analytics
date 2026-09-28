using System;
using System.Data.Common;
using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Infrastructure.Data;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Repositories
{
    public sealed class AnalyticsStoreRepository : IAnalyticsStoreRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public AnalyticsStoreRepository(IDbConnectionFactory connectionFactory)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException("connectionFactory");
            }

            _connectionFactory = connectionFactory;
        }

        public async Task<AnalyticsStoreStatus> GetStatusAsync()
        {
            using (DbConnection connection = _connectionFactory.CreateConnection())
            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)) AS ServerName,
    DB_NAME() AS DatabaseName,
    (SELECT COUNT_BIG(1) FROM dbo.VideoMetadata) AS VideoCount,
    (SELECT COUNT_BIG(1) FROM dbo.ChannelAnalyticsDaily) +
        (SELECT COUNT_BIG(1) FROM dbo.VideoAnalyticsDaily) +
        (SELECT COUNT_BIG(1) FROM dbo.TrafficSourceAnalyticsDaily) AS SnapshotCount,
    (SELECT MAX(CompletedAtUtc)
       FROM dbo.AnalyticsSyncLog
      WHERE Status = 'Completed' AND SyncType LIKE 'YouTube-%') AS LastSuccessfulSyncUtc,
    state.IsRunning,
    state.OldestSyncedDate,
    state.LatestSyncedDate,
    state.LastErrorMessage
FROM dbo.AnalyticsSyncState state
WHERE state.Id = 1;";
                command.CommandTimeout = 10;

                await connection.OpenAsync().ConfigureAwait(false);
                using (DbDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync().ConfigureAwait(false))
                    {
                        throw new InvalidOperationException("The analytics database did not return its status.");
                    }

                    return new AnalyticsStoreStatus
                    {
                        ServerName = reader.GetString(0),
                        DatabaseName = reader.GetString(1),
                        VideoCount = reader.GetInt64(2),
                        SnapshotCount = reader.GetInt64(3),
                        LastSuccessfulSyncUtc = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                        IsSyncRunning = reader.GetBoolean(5),
                        OldestSyncedDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                        LatestSyncedDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                        LastSyncError = reader.IsDBNull(8) ? null : reader.GetString(8)
                    };
                }
            }
        }
    }
}
