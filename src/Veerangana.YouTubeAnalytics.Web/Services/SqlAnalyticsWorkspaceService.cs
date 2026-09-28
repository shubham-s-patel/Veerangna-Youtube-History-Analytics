using System;
using System.Threading.Tasks;
using Veerangana.YouTubeAnalytics.Models;
using Veerangana.YouTubeAnalytics.Repositories;
using Veerangana.YouTubeAnalytics.ViewModels;

namespace Veerangana.YouTubeAnalytics.Services
{
    public sealed class SqlAnalyticsWorkspaceService : IAnalyticsWorkspaceService
    {
        private readonly IAnalyticsFactRepository _repository;

        public SqlAnalyticsWorkspaceService(IAnalyticsFactRepository repository)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            _repository = repository;
        }

        public Task<AnalyticsReportData> GetReportAsync(
            AnalyticsReportKind reportKind,
            GlobalFilterViewModel filters)
        {
            return _repository.GetReportAsync(reportKind, filters);
        }
    }
}
