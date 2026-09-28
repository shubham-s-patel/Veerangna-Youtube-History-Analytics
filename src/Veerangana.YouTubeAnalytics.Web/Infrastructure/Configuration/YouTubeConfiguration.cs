using System;
using System.Configuration;

namespace Veerangana.YouTubeAnalytics.Infrastructure.Configuration
{
    public sealed class YouTubeConfiguration : IYouTubeConfiguration
    {
        public YouTubeConfiguration()
        {
            ApplicationName = Read("YouTubeApplicationName", "VEERANGANA_YOUTUBE_APPLICATION_NAME");
            ClientId = Read("YouTubeClientId", "VEERANGANA_YOUTUBE_CLIENT_ID");
            ClientSecret = Read("YouTubeClientSecret", "VEERANGANA_YOUTUBE_CLIENT_SECRET");
            RefreshToken = Read("YouTubeRefreshToken", "VEERANGANA_YOUTUBE_REFRESH_TOKEN");
            ApiKey = Read("YouTubeApiKey", "VEERANGANA_YOUTUBE_API_KEY");
            ChannelId = Read("YouTubeChannelId", "VEERANGANA_YOUTUBE_CHANNEL_ID");
            ChannelHandle = Read("YouTubeChannelHandle", "VEERANGANA_YOUTUBE_CHANNEL_HANDLE");
            ChannelUrl = Read("YouTubeChannelUrl", "VEERANGANA_YOUTUBE_CHANNEL_URL");
            OAuthRedirectUri = Read("YouTubeOAuthRedirectUri", "VEERANGANA_YOUTUBE_OAUTH_REDIRECT_URI");
            Scopes = Read("YouTubeOAuthScopes", "VEERANGANA_YOUTUBE_OAUTH_SCOPES");
        }

        public string ApplicationName { get; private set; }

        public string ClientId { get; private set; }

        public string ClientSecret { get; private set; }

        public string RefreshToken { get; private set; }

        public string ApiKey { get; private set; }

        public string ChannelId { get; private set; }

        public string ChannelHandle { get; private set; }

        public string ChannelUrl { get; private set; }

        public string OAuthRedirectUri { get; private set; }

        public string Scopes { get; private set; }

        public bool HasClientCredentials
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ClientId) &&
                       !string.IsNullOrWhiteSpace(ClientSecret);
            }
        }

        public bool HasRefreshToken
        {
            get { return !string.IsNullOrWhiteSpace(RefreshToken); }
        }

        public bool HasChannelIdentity
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ChannelId) ||
                       !string.IsNullOrWhiteSpace(ChannelHandle);
            }
        }

        public bool IsAnalyticsReady
        {
            get { return HasClientCredentials && HasRefreshToken && HasChannelIdentity; }
        }

        private static string Read(string appSettingKey, string environmentVariableName)
        {
            string environmentValue = Environment.GetEnvironmentVariable(environmentVariableName);
            return !string.IsNullOrWhiteSpace(environmentValue)
                ? environmentValue.Trim()
                : (ConfigurationManager.AppSettings[appSettingKey] ?? string.Empty).Trim();
        }
    }
}
