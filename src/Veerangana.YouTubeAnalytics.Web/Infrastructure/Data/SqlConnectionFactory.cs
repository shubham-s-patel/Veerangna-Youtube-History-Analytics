using System.Configuration;
using System.Data.Common;
using System.Data.SqlClient;

namespace Veerangana.YouTubeAnalytics.Infrastructure.Data
{
    public sealed class SqlConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public SqlConnectionFactory(string connectionStringName = "VeeranganaAnalyticsDb")
        {
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[connectionStringName];

            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new ConfigurationErrorsException(
                    "The configured analytics database connection string was not found.");
            }

            _connectionString = settings.ConnectionString;
        }

        public DbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
