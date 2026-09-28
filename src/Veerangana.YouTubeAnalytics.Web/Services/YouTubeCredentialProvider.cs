using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Veerangana.YouTubeAnalytics.Infrastructure.Configuration;

namespace Veerangana.YouTubeAnalytics.Services
{
    public sealed class YouTubeCredentialProvider : IYouTubeCredentialProvider
    {
        private readonly IYouTubeConfiguration _configuration;
        private readonly object _syncRoot = new object();
        private Task<UserCredential> _credentialTask;

        public YouTubeCredentialProvider(IYouTubeConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            _configuration = configuration;
        }

        public Task<UserCredential> GetCredentialAsync()
        {
            lock (_syncRoot)
            {
                return _credentialTask ?? (_credentialTask = CreateCredentialAsync());
            }
        }

        private async Task<UserCredential> CreateCredentialAsync()
        {
            if (!_configuration.HasClientCredentials || !_configuration.HasRefreshToken)
            {
                throw new ConfigurationErrorsException(
                    "YouTube OAuth client credentials and refresh token are required.");
            }

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _configuration.ClientId,
                    ClientSecret = _configuration.ClientSecret
                },
                Scopes = (_configuration.Scopes ?? string.Empty).Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries)
            });

            var token = new TokenResponse
            {
                RefreshToken = _configuration.RefreshToken
            };

            var credential = new UserCredential(flow, "veerangana-analytics", token);
            bool refreshed = await credential.RefreshTokenAsync(CancellationToken.None).ConfigureAwait(false);

            if (!refreshed)
            {
                throw new InvalidOperationException("YouTube OAuth access could not be refreshed.");
            }

            return credential;
        }
    }
}
