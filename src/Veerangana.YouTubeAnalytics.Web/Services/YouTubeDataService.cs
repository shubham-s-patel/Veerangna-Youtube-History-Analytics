using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Veerangana.YouTubeAnalytics.Infrastructure.Configuration;
using Veerangana.YouTubeAnalytics.Models;

namespace Veerangana.YouTubeAnalytics.Services
{
    public sealed class YouTubeDataService : IYouTubeDataService
    {
        private readonly IYouTubeCredentialProvider _credentialProvider;
        private readonly IYouTubeConfiguration _configuration;

        public YouTubeDataService(
            IYouTubeCredentialProvider credentialProvider,
            IYouTubeConfiguration configuration)
        {
            if (credentialProvider == null)
            {
                throw new ArgumentNullException("credentialProvider");
            }

            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            _credentialProvider = credentialProvider;
            _configuration = configuration;
        }

        public async Task<YouTubeChannelInfo> GetAuthorizedChannelAsync()
        {
            using (YouTubeService service = await CreateServiceAsync().ConfigureAwait(false))
            {
                ChannelsResource.ListRequest request = service.Channels.List(
                    new[] { "snippet", "statistics" });
                request.Mine = true;

                ChannelListResponse response = await request.ExecuteAsync().ConfigureAwait(false);
                Channel channel = response.Items == null ? null : response.Items.FirstOrDefault();

                if (channel == null)
                {
                    throw new InvalidOperationException("The authorized Google account has no YouTube channel.");
                }

                if (!string.IsNullOrWhiteSpace(_configuration.ChannelId) &&
                    !string.Equals(channel.Id, _configuration.ChannelId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The refresh token belongs to a different YouTube channel.");
                }

                return new YouTubeChannelInfo
                {
                    Id = channel.Id,
                    Title = channel.Snippet == null ? string.Empty : channel.Snippet.Title,
                    AvatarUrl = channel.Snippet == null ? string.Empty : GetThumbnailUrl(channel.Snippet.Thumbnails),
                    PublishedAtUtc = channel.Snippet == null || !channel.Snippet.PublishedAtDateTimeOffset.HasValue
                        ? DateTime.UtcNow.Date
                        : channel.Snippet.PublishedAtDateTimeOffset.Value.UtcDateTime,
                    ViewCount = ToInt64(channel.Statistics == null ? null : channel.Statistics.ViewCount),
                    SubscriberCount = channel.Statistics == null || !channel.Statistics.SubscriberCount.HasValue
                        ? (long?)null
                        : ToInt64(channel.Statistics.SubscriberCount),
                    VideoCount = (int)Math.Min(
                        int.MaxValue,
                        ToInt64(channel.Statistics == null ? null : channel.Statistics.VideoCount))
                };
            }
        }

        public async Task<IList<YouTubeVideoInfo>> GetVideosAsync(IEnumerable<string> videoIds)
        {
            string[] ids = (videoIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (ids.Length == 0)
            {
                return new List<YouTubeVideoInfo>();
            }

            using (YouTubeService service = await CreateServiceAsync().ConfigureAwait(false))
            {
                var videos = new List<YouTubeVideoInfo>();
                for (int offset = 0; offset < ids.Length; offset += 50)
                {
                    string[] batch = ids.Skip(offset).Take(50).ToArray();
                    VideosResource.ListRequest request = service.Videos.List(
                        new[] { "snippet", "contentDetails", "statistics", "liveStreamingDetails" });
                    request.Id = batch;

                    VideoListResponse response = await request.ExecuteAsync().ConfigureAwait(false);
                    videos.AddRange((response.Items ?? new List<Video>()).Select(MapVideo));
                }

                return videos;
            }
        }

        private async Task<YouTubeService> CreateServiceAsync()
        {
            UserCredential credential = await _credentialProvider.GetCredentialAsync().ConfigureAwait(false);
            return new YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _configuration.ApplicationName
            });
        }

        private static YouTubeVideoInfo MapVideo(Video video)
        {
            int durationSeconds = 0;
            if (video.ContentDetails != null && !string.IsNullOrWhiteSpace(video.ContentDetails.Duration))
            {
                durationSeconds = (int)Math.Min(
                    int.MaxValue,
                    Math.Max(0, XmlConvert.ToTimeSpan(video.ContentDetails.Duration).TotalSeconds));
            }

            return new YouTubeVideoInfo
            {
                Id = video.Id,
                ChannelId = video.Snippet == null ? string.Empty : video.Snippet.ChannelId,
                Title = video.Snippet == null ? string.Empty : video.Snippet.Title,
                ThumbnailUrl = video.Snippet == null ? string.Empty : GetThumbnailUrl(video.Snippet.Thumbnails),
                PublishedAtUtc = video.Snippet == null || !video.Snippet.PublishedAtDateTimeOffset.HasValue
                    ? DateTime.MinValue
                    : video.Snippet.PublishedAtDateTimeOffset.Value.UtcDateTime,
                DurationSeconds = durationSeconds,
                IsLiveStream = video.LiveStreamingDetails != null,
                ViewCount = ToInt64(video.Statistics == null ? null : video.Statistics.ViewCount),
                LikeCount = ToInt64(video.Statistics == null ? null : video.Statistics.LikeCount),
                CommentCount = ToInt64(video.Statistics == null ? null : video.Statistics.CommentCount)
            };
        }

        private static string GetThumbnailUrl(ThumbnailDetails thumbnails)
        {
            if (thumbnails == null)
            {
                return string.Empty;
            }

            Thumbnail thumbnail = thumbnails.Maxres ??
                                  thumbnails.Standard ??
                                  thumbnails.High ??
                                  thumbnails.Medium ??
                                  thumbnails.Default__;

            return thumbnail == null ? string.Empty : thumbnail.Url;
        }

        private static long ToInt64(ulong? value)
        {
            return value.HasValue
                ? (long)Math.Min((ulong)long.MaxValue, value.Value)
                : 0L;
        }
    }
}
