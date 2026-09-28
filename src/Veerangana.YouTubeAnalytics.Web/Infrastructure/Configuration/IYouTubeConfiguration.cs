namespace Veerangana.YouTubeAnalytics.Infrastructure.Configuration
{
    public interface IYouTubeConfiguration
    {
        string ApplicationName { get; }

        string ClientId { get; }

        string ClientSecret { get; }

        string RefreshToken { get; }

        string ApiKey { get; }

        string ChannelId { get; }

        string ChannelHandle { get; }

        string ChannelUrl { get; }

        string OAuthRedirectUri { get; }

        string Scopes { get; }

        bool HasClientCredentials { get; }

        bool HasRefreshToken { get; }

        bool HasChannelIdentity { get; }

        bool IsAnalyticsReady { get; }
    }
}
