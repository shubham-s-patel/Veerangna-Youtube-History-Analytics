namespace Veerangana.YouTubeAnalytics.ViewModels
{
    public sealed class YouTubeConfigurationStatusViewModel
    {
        public bool IsAnalyticsReady { get; set; }

        public bool HasClientCredentials { get; set; }

        public bool HasRefreshToken { get; set; }

        public bool HasApiKey { get; set; }

        public bool HasChannelIdentity { get; set; }

        public string ChannelIdentity { get; set; }

        public string ChannelUrl { get; set; }
    }
}
