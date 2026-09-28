using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Veerangana.YouTubeAnalytics.Infrastructure.Data
{
    public sealed class AnalyticsDatabaseInitializer
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public AnalyticsDatabaseInitializer(IDbConnectionFactory connectionFactory)
        {
            if (connectionFactory == null) throw new ArgumentNullException("connectionFactory");
            _connectionFactory = connectionFactory;
        }

        public void Initialize()
        {
            string migrationDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Database");

            if (!Directory.Exists(migrationDirectory))
            {
                throw new DirectoryNotFoundException("Analytics database migrations were not found.");
            }

            string[] migrationPaths = Directory.GetFiles(migrationDirectory, "*.sql")
                .Where(path => MigrationVersion(path) >= 2)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (migrationPaths.Length == 0)
            {
                throw new FileNotFoundException("Analytics database migrations were not found.");
            }

            using (DbConnection connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                foreach (string migrationPath in migrationPaths)
                {
                    string script = File.ReadAllText(migrationPath);
                    string[] batches = Regex.Split(
                        script,
                        @"^\s*GO\s*;?\s*$",
                        RegexOptions.Multiline | RegexOptions.IgnoreCase);

                    foreach (string batch in batches)
                    {
                        if (string.IsNullOrWhiteSpace(batch)) continue;
                        using (DbCommand command = connection.CreateCommand())
                        {
                            command.CommandText = batch;
                            command.CommandTimeout = 60;
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        private static int MigrationVersion(string path)
        {
            string fileName = Path.GetFileName(path);
            int version;
            return fileName.Length >= 3 && int.TryParse(fileName.Substring(0, 3), out version)
                ? version
                : 0;
        }
    }
}
