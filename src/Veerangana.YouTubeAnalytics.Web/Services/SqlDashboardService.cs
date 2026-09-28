using System;
using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Models;
using Veerangana.YouTubeAnalytics.Repositories;
using Veerangana.YouTubeAnalytics.ViewModels;

namespace Veerangana.YouTubeAnalytics.Services
{
    public sealed class SqlDashboardService : IDashboardService
    {
        private readonly IAnalyticsFactRepository _repository;

        public SqlDashboardService(IAnalyticsFactRepository repository)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            _repository = repository;
        }

        public Task<DashboardOverviewData> GetOverviewAsync(GlobalFilterViewModel filters)
        {
            return _repository.GetOverviewAsync(filters);
        }
    }
}
