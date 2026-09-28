using System.Data.Common;

namespace Veerangana.YouTubeAnalytics.Infrastructure.Data
{
    public interface IDbConnectionFactory
    {
        DbConnection CreateConnection();
    }
}
