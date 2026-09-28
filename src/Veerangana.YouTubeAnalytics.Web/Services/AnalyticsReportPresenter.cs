using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Veerangana.YouTubeAnalytics.Models;
using Veerangana.YouTubeAnalytics.ViewModels;

namespace Veerangana.YouTubeAnalytics.Services
{
    public static class AnalyticsReportPresenter
    {
        private static readonly IDictionary<string, string> TrafficSourceNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ADVERTISING", "YouTube advertising" },
                { "ANNOTATION", "Video annotations" },
                { "CAMPAIGN_CARD", "Campaign cards" },
                { "CHANNEL", "Channel pages" },
                { "END_SCREEN", "End screens" },
                { "EXT_URL", "External websites and apps" },
                { "HASHTAGS", "Hashtag pages" },
                { "LIVE_REDIRECT", "Live redirects" },
                { "NO_LINK_EMBEDDED", "Embedded player" },
                { "NO_LINK_OTHER", "Direct or unknown" },
                { "NOTIFICATION", "Notifications" },
                { "PLAYLIST", "Playlists" },
                { "PRODUCT_PAGE", "YouTube product pages" },
                { "PROMOTED", "Promoted content" },
                { "RELATED_VIDEO", "Suggested videos" },
                { "SHORTS", "Shorts feed" },
                { "SOUND_PAGE", "Sound pages" },
                { "SUBSCRIBER", "Subscriptions feed" },
                { "YT_CHANNEL", "Channel pages" },
                { "YT_OTHER_PAGE", "Other YouTube features" },
                { "YT_SEARCH", "YouTube search" }
            };

        public static void Populate(
            AnalyticsReportViewModel model,
            AnalyticsReportData data,
            AnalyticsReportKind reportKind)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (data == null) throw new ArgumentNullException("data");

            model.HasData = true;
            model.WorkspaceName = data.Channel.Title;
            model.WorkspaceAvatarUrl = data.Channel.AvatarUrl;
            model.LastUpdatedUtc = data.Channel.LastSyncedAtUtc;
            model.PeriodLabel = string.Format(
                CultureInfo.InvariantCulture,
                "{0:dd MMM yyyy} - {1:dd MMM yyyy}",
                data.Period.StartDate,
                data.Period.EndDate);
            model.SectionCssClass = "report-theme--" + reportKind.ToString().ToLowerInvariant();

            Configure(model, data, reportKind);
            model.Insights = BuildInsights(data, reportKind);
        }

        private static void Configure(
            AnalyticsReportViewModel model,
            AnalyticsReportData data,
            AnalyticsReportKind reportKind)
        {
            switch (reportKind)
            {
                case AnalyticsReportKind.Videos:
                    ConfigureVideos(model, data);
                    break;
                case AnalyticsReportKind.Consumption:
                    ConfigureConsumption(model, data);
                    break;
                case AnalyticsReportKind.Audience:
                    ConfigureAudience(model, data);
                    break;
                case AnalyticsReportKind.Engagement:
                    ConfigureEngagement(model, data);
                    break;
                case AnalyticsReportKind.TrafficSources:
                    ConfigureTrafficSources(model, data);
                    break;
                case AnalyticsReportKind.Geography:
                    ConfigureGeography(model, data);
                    break;
                case AnalyticsReportKind.Subscribers:
                    ConfigureSubscribers(model, data);
                    break;
                default:
                    ConfigureReports(model, data);
                    break;
            }
        }

        private static void ConfigureVideos(AnalyticsReportViewModel model, AnalyticsReportData data)
        {
            double topShare = data.Current.Views == 0 || data.TopVideos.Count == 0
                ? 0d
                : data.TopVideos[0].Analytics.Views * 100d / data.Current.Views;
            model.Metrics = new List<MetricCardViewModel>
            {
                Metric("Active videos", FormatNumber(data.TopVideos.Count), "Videos with activity in this period",
                    "fa fa-film", "metric-icon--rose", data.TopVideos.Count, null),
                Metric("Video views", FormatNumber(data.Current.Views), "Views across analyzed videos",
                    "fa fa-eye", "metric-icon--teal", data.Current.Views, Previous(data.Previous, x => x.Views)),
                Metric("Watch time", FormatHours(data.Current.WatchTimeMinutes), "Total viewing time",
                    "fa fa-clock-o", "metric-icon--gold", data.Current.WatchTimeMinutes,
                    Previous(data.Previous, x => x.WatchTimeMinutes)),
                Metric("Top video share", FormatPercent(topShare), "Share of period views from the leading video",
                    "fa fa-trophy", "metric-icon--blue", topShare, null)
            };

            ConfigureVideoChart(model, data, "Library distribution", "Views by active video");
            ConfigureVideoTable(model, data, "Library", "Videos available for analysis", "Avg. viewed", "Published",
                record => FormatPercent(record.Analytics.AverageViewPercentage),
                record => record.Video.PublishedAtUtc.ToString("dd MMM yyyy", CultureInfo.InvariantCulture));
            model.EmptyMessage = "No videos generated activity for these filters.";
        }

        private static void ConfigureConsumption(AnalyticsReportViewModel model, AnalyticsReportData data)
        {
            model.Metrics = ConsumptionMetrics(data);
            ConfigureDailyChart(model, data, "Consumption", "Daily attention", false);
            ConfigureVideoTable(model, data, "Viewing depth", "Videos holding attention", "Avg. duration", "Avg. viewed",
                record => FormatDuration(record.Analytics.AverageViewDurationSeconds),
                record => FormatPercent(record.Analytics.AverageViewPercentage));
            model.EmptyMessage = "No consumption data exists for these filters.";
        }

        private static void ConfigureAudience(AnalyticsReportViewModel model, AnalyticsReportData data)
        {
            long netSubscribers = NetSubscribers(data.Current);
            model.Metrics = new List<MetricCardViewModel>
            {
                Metric("Audience views", FormatNumber(data.Current.Views), "Views from the selected audience",
                    "fa fa-eye", "metric-icon--rose", data.Current.Views, Previous(data.Previous, x => x.Views)),
                Metric("Watch time", FormatHours(data.Current.WatchTimeMinutes), "Time invested by viewers",
                    "fa fa-clock-o", "metric-icon--teal", data.Current.WatchTimeMinutes,
                    Previous(data.Previous, x => x.WatchTimeMinutes)),
                Metric("Avg. viewed", FormatPercent(data.Current.AverageViewPercentage), "Average viewing depth",
                    "fa fa-percent", "metric-icon--gold", data.Current.AverageViewPercentage,
                    Previous(data.Previous, x => x.AverageViewPercentage)),
                Metric("Net audience", FormatSignedNumber(netSubscribers), "Subscribers gained minus subscribers lost",
                    "fa fa-users", "metric-icon--blue", netSubscribers,
                    data.Previous == null ? (double?)null : NetSubscribers(data.Previous))
            };

            ConfigureDailyChart(model, data, "Viewer behavior", "Daily audience activity", false);
            ConfigureVideoTable(model, data, "Audience choices", "Videos shaping viewer behavior", "Avg. viewed", "Net audience",
                record => FormatPercent(record.Analytics.AverageViewPercentage),
                record => FormatSignedNumber(record.Analytics.SubscribersGained - record.Analytics.SubscribersLost));
            model.EmptyMessage = "No viewer behavior data exists for these filters.";
        }

        private static void ConfigureEngagement(AnalyticsReportViewModel model, AnalyticsReportData data)
        {
            long interactions = data.Current.Likes + data.Current.Comments + data.Current.Shares;
            double engagementRate = Rate(interactions, data.Current.Views);
            model.Metrics = new List<MetricCardViewModel>
            {
                Metric("Likes", FormatNumber(data.Current.Likes), "Likes received in this period",
                    "fa fa-thumbs-o-up", "metric-icon--rose", data.Current.Likes,
                    Previous(data.Previous, x => x.Likes)),
                Metric("Comments", FormatNumber(data.Current.Comments), "Viewer conversations",
                    "fa fa-comments-o", "metric-icon--teal", data.Current.Comments,
                    Previous(data.Previous, x => x.Comments)),
                Metric("Shares", FormatNumber(data.Current.Shares), "Times viewers shared videos",
                    "fa fa-share-alt", "metric-icon--gold", data.Current.Shares,
                    Previous(data.Previous, x => x.Shares)),
                Metric("Interaction rate", FormatPercent(engagementRate), "Interactions as a share of views",
                    "fa fa-heart-o", "metric-icon--blue", engagementRate,
                    data.Previous == null ? (double?)null : Rate(
                        data.Previous.Likes + data.Previous.Comments + data.Previous.Shares,
                        data.Previous.Views))
            };

            ConfigureDailyChart(model, data, "Response context", "Attention surrounding interactions", false);
            ConfigureVideoTable(model, data, "Interaction leaders", "Videos creating response", "Comments", "Shares",
                record => FormatNumber(record.Analytics.Comments),
                record => FormatNumber(record.Analytics.Shares),
                "Likes");
            model.EmptyMessage = "No interaction data exists for these filters.";
        }

        private static void ConfigureTrafficSources(AnalyticsReportViewModel model, AnalyticsReportData data)
        {
            AnalyticsBreakdownMetric top = data.Breakdown.FirstOrDefault();
            double topShare = top == null ? 0d : Rate(top.Views, data.Current.Views);
            model.Metrics = new List<MetricCardViewModel>
            {
                Metric("Discovery paths", FormatNumber(data.Breakdown.Count), "Sources producing views",
                    "fa fa-random", "metric-icon--rose", data.Breakdown.Count, null),
                Metric("Discovered views", FormatNumber(data.Current.Views), "Views from selected discovery paths",
                    "fa fa-eye", "metric-icon--teal", data.Current.Views, Previous(data.Previous, x => x.Views)),
                Metric("Watch time", FormatHours(data.Current.WatchTimeMinutes), "Viewing time from discovery",
                    "fa fa-clock-o", "metric-icon--gold", data.Current.WatchTimeMinutes,
                    Previous(data.Previous, x => x.WatchTimeMinutes)),
                Metric("Leading path share", FormatPercent(topShare), "Share generated by the leading source",
                    "fa fa-compass", "metric-icon--blue", topShare, null)
            };

            ConfigureBreakdown(model, data, true);
            model.EmptyMessage = "No discovery-path data exists for these filters.";
        }

        private static void ConfigureGeography(AnalyticsReportViewModel model, AnalyticsReportData data)
        {
            AnalyticsBreakdownMetric top = data.Breakdown.FirstOrDefault();
            double topShare = top == null ? 0d : Rate(top.Views, data.Current.Views);
            model.Metrics = new List<MetricCardViewModel>
            {
                Metric("Active markets", FormatNumber(data.Breakdown.Count), "Countries producing views",
                    "fa fa-globe", "metric-icon--rose", data.Breakdown.Count, null),
                Metric("Market views", FormatNumber(data.Current.Views), "Views across selected markets",
                    "fa fa-eye", "metric-icon--teal", data.Current.Views, Previous(data.Previous, x => x.Views)),
                Metric("Watch time", FormatHours(data.Current.WatchTimeMinutes), "Viewing time across markets",
                    "fa fa-clock-o", "metric-icon--gold", data.Current.WatchTimeMinutes,
                    Previous(data.Previous, x => x.WatchTimeMinutes)),
                Metric("Top market share", FormatPercent(topShare), "Share generated by the leading market",
                    "fa fa-map-marker", "metric-icon--blue", topShare, null)
            };

            ConfigureBreakdown(model, data, false);
            model.EmptyMessage = "No market data exists for these filters.";
        }

        private static void ConfigureSubscribers(AnalyticsReportViewModel model, AnalyticsReportData data)
        {
            long net = NetSubscribers(data.Current);
            double conversion = data.Current.Views == 0 ? 0d : net * 1000d / data.Current.Views;
            model.Metrics = new List<MetricCardViewModel>
            {
                Metric("Subscribers gained", FormatSignedNumber(data.Current.SubscribersGained), "Audience gained from videos",
                    "fa fa-user-plus", "metric-icon--rose", data.Current.SubscribersGained,
                    Previous(data.Previous, x => x.SubscribersGained)),
                Metric("Subscribers lost", FormatNumber(data.Current.SubscribersLost), "Audience lost in this period",
                    "fa fa-user-times", "metric-icon--teal", data.Current.SubscribersLost,
                    Previous(data.Previous, x => x.SubscribersLost)),
                Metric("Net audience", FormatSignedNumber(net), "Subscribers gained minus subscribers lost",
                    "fa fa-users", "metric-icon--gold", net,
                    data.Previous == null ? (double?)null : NetSubscribers(data.Previous)),
                Metric("Net per 1K views", conversion.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture),
                    "Net subscribers per thousand views", "fa fa-exchange", "metric-icon--blue", conversion, null)
            };

            ConfigureDailyChart(model, data, "Audience growth", "Daily subscriber movement", true);
            ConfigureSubscriberTable(model, data);
            model.EmptyMessage = "No audience-growth data exists for these filters.";
        }

        private static void ConfigureReports(AnalyticsReportViewModel model, AnalyticsReportData data)
        {
            model.Metrics = ConsumptionMetrics(data);
            ConfigureDailyChart(model, data, "Period report", "Video use across the reporting period", false);
            ConfigureVideoTable(model, data, "Report detail", "Leading contributions", "Interactions", "Net audience",
                record => FormatNumber(record.Analytics.Likes + record.Analytics.Comments + record.Analytics.Shares),
                record => FormatSignedNumber(record.Analytics.SubscribersGained - record.Analytics.SubscribersLost));
            model.EmptyMessage = "No report data exists for these filters.";
        }

        private static IList<MetricCardViewModel> ConsumptionMetrics(AnalyticsReportData data)
        {
            return new List<MetricCardViewModel>
            {
                Metric("Video views", FormatNumber(data.Current.Views), "Views generated by analyzed videos",
                    "fa fa-eye", "metric-icon--rose", data.Current.Views, Previous(data.Previous, x => x.Views)),
                Metric("Watch time", FormatHours(data.Current.WatchTimeMinutes), "Time spent watching videos",
                    "fa fa-clock-o", "metric-icon--teal", data.Current.WatchTimeMinutes,
                    Previous(data.Previous, x => x.WatchTimeMinutes)),
                Metric("Avg. view duration", FormatDuration(data.Current.AverageViewDurationSeconds),
                    "Average time watched per view", "fa fa-hourglass-half", "metric-icon--gold",
                    data.Current.AverageViewDurationSeconds,
                    Previous(data.Previous, x => x.AverageViewDurationSeconds)),
                Metric("Avg. percentage viewed", FormatPercent(data.Current.AverageViewPercentage),
                    "Average portion of a video watched", "fa fa-percent", "metric-icon--blue",
                    data.Current.AverageViewPercentage,
                    Previous(data.Previous, x => x.AverageViewPercentage))
            };
        }

        private static void ConfigureDailyChart(
            AnalyticsReportViewModel model,
            AnalyticsReportData data,
            string kicker,
            string title,
            bool subscriberSeries)
        {
            model.ChartKicker = kicker;
            model.ChartTitle = title;
            model.ChartType = subscriberSeries ? "bar" : "line";
            model.PrimarySeriesLabel = subscriberSeries ? "Subscribers gained" : "Views";
            model.SecondarySeriesLabel = subscriberSeries ? "Subscribers lost" : "Watch hours";
            model.ChartPoints = data.Trend.Select(point => new ReportChartPointViewModel
            {
                Label = point.Date.ToString("dd MMM", CultureInfo.InvariantCulture),
                PrimaryValue = subscriberSeries ? point.SubscribersGained : point.Views,
                SecondaryValue = subscriberSeries ? point.SubscribersLost : Math.Round(point.WatchTimeMinutes / 60d, 2)
            }).ToList();
        }

        private static void ConfigureVideoChart(
            AnalyticsReportViewModel model,
            AnalyticsReportData data,
            string kicker,
            string title)
        {
            model.ChartKicker = kicker;
            model.ChartTitle = title;
            model.ChartType = "bar";
            model.PrimarySeriesLabel = "Views";
            model.SecondarySeriesLabel = "Watch hours";
            model.ChartPoints = data.TopVideos.Take(10).Select(record => new ReportChartPointViewModel
            {
                Label = ShortLabel(record.Video.Title),
                PrimaryValue = record.Analytics.Views,
                SecondaryValue = Math.Round(record.Analytics.WatchTimeMinutes / 60d, 2)
            }).ToList();
        }

        private static void ConfigureBreakdown(
            AnalyticsReportViewModel model,
            AnalyticsReportData data,
            bool trafficSources)
        {
            model.ChartKicker = trafficSources ? "Discovery mix" : "Market distribution";
            model.ChartTitle = trafficSources ? "Views by discovery path" : "Views by market";
            model.ChartType = "bar";
            model.PrimarySeriesLabel = "Views";
            model.SecondarySeriesLabel = "Watch hours";
            model.ChartPoints = data.Breakdown.Take(10).Select(row => new ReportChartPointViewModel
            {
                Label = trafficSources ? TrafficSourceLabel(row.Key) : CountryLabel(row.Key),
                PrimaryValue = row.Views,
                SecondaryValue = Math.Round(row.WatchTimeMinutes / 60d, 2)
            }).ToList();

            model.TableKicker = trafficSources ? "Acquisition" : "Geography";
            model.TableTitle = trafficSources ? "Discovery-path performance" : "Market performance";
            model.FirstColumnLabel = trafficSources ? "Discovery path" : "Market";
            model.Value1Label = "Views";
            model.Value2Label = "Share";
            model.Value3Label = "Watch time";
            model.Value4Label = "Avg. viewed";
            model.Rows = data.Breakdown.Select(row => new AnalyticsReportRowViewModel
            {
                Label = trafficSources ? TrafficSourceLabel(row.Key) : CountryLabel(row.Key),
                Subtitle = trafficSources ? "Traffic source" : row.Key.ToUpperInvariant(),
                Badge = trafficSources ? null : row.Key.ToUpperInvariant(),
                Value1 = FormatNumber(row.Views),
                Value2 = FormatPercent(Rate(row.Views, data.Current.Views)),
                Value3 = FormatHours(row.WatchTimeMinutes),
                Value4 = FormatPercent(row.AverageViewPercentage)
            }).ToList();
        }

        private static void ConfigureVideoTable(
            AnalyticsReportViewModel model,
            string kicker,
            string title,
            string value3Label,
            string value4Label,
            Func<VideoUsageRecord, string> value3,
            Func<VideoUsageRecord, string> value4,
            string value1Label = "Views")
        {
            model.TableKicker = kicker;
            model.TableTitle = title;
            model.FirstColumnLabel = "Video";
            model.Value1Label = value1Label;
            model.Value2Label = value1Label == "Likes" ? "Interaction rate" : "Watch time";
            model.Value3Label = value3Label;
            model.Value4Label = value4Label;
            model.Rows = model.Rows ?? new List<AnalyticsReportRowViewModel>();
        }

        private static void ConfigureVideoTable(
            AnalyticsReportViewModel model,
            AnalyticsReportData data,
            string kicker,
            string title,
            string value3Label,
            string value4Label,
            Func<VideoUsageRecord, string> value3,
            Func<VideoUsageRecord, string> value4,
            string value1Label = "Views")
        {
            ConfigureVideoTable(model, kicker, title, value3Label, value4Label, value3, value4, value1Label);
            model.Rows = data.TopVideos.Select(record => new AnalyticsReportRowViewModel
            {
                Label = record.Video.Title,
                Subtitle = "Published " + record.Video.PublishedAtUtc.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
                ImageUrl = record.Video.ThumbnailUrl,
                LinkUrl = "https://www.youtube.com/watch?v=" + record.Video.Id,
                Badge = FormatDuration(record.Video.DurationSeconds),
                Value1 = value1Label == "Likes" ? FormatNumber(record.Analytics.Likes) : FormatNumber(record.Analytics.Views),
                Value2 = value1Label == "Likes"
                    ? FormatPercent(Rate(
                        record.Analytics.Likes + record.Analytics.Comments + record.Analytics.Shares,
                        record.Analytics.Views))
                    : FormatHours(record.Analytics.WatchTimeMinutes),
                Value3 = value3(record),
                Value4 = value4(record)
            }).ToList();
        }

        private static void ConfigureSubscriberTable(AnalyticsReportViewModel model, AnalyticsReportData data)
        {
            model.TableKicker = "Contribution";
            model.TableTitle = "Videos growing the audience";
            model.FirstColumnLabel = "Video";
            model.Value1Label = "Gained";
            model.Value2Label = "Lost";
            model.Value3Label = "Net";
            model.Value4Label = "Views";
            model.Rows = data.TopVideos.Select(record => new AnalyticsReportRowViewModel
            {
                Label = record.Video.Title,
                Subtitle = "Published " + record.Video.PublishedAtUtc.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
                ImageUrl = record.Video.ThumbnailUrl,
                LinkUrl = "https://www.youtube.com/watch?v=" + record.Video.Id,
                Badge = FormatDuration(record.Video.DurationSeconds),
                Value1 = FormatSignedNumber(record.Analytics.SubscribersGained),
                Value2 = FormatNumber(record.Analytics.SubscribersLost),
                Value3 = FormatSignedNumber(record.Analytics.SubscribersGained - record.Analytics.SubscribersLost),
                Value4 = FormatNumber(record.Analytics.Views)
            }).ToList();
        }

        private static IList<InsightViewModel> BuildInsights(
            AnalyticsReportData data,
            AnalyticsReportKind reportKind)
        {
            var insights = new List<InsightViewModel>();
            VideoUsageRecord topVideo = data.TopVideos.FirstOrDefault();
            AnalyticsBreakdownMetric topBreakdown = data.Breakdown.FirstOrDefault();

            if (topVideo != null)
            {
                insights.Add(Insight("fa fa-bolt", "insight-item__icon--rose", "Leading video",
                    topVideo.Video.Title + " generated " + FormatNumber(topVideo.Analytics.Views) + " views."));
            }
            else if (topBreakdown != null)
            {
                string label = reportKind == AnalyticsReportKind.Geography
                    ? CountryLabel(topBreakdown.Key)
                    : TrafficSourceLabel(topBreakdown.Key);
                insights.Add(Insight("fa fa-bolt", "insight-item__icon--rose", "Leading contributor",
                    label + " generated " + FormatNumber(topBreakdown.Views) + " views."));
            }

            insights.Add(Insight("fa fa-clock-o", "insight-item__icon--teal", "Viewing depth",
                "Average view duration was " + FormatDuration(data.Current.AverageViewDurationSeconds) +
                " with " + FormatPercent(data.Current.AverageViewPercentage) + " viewed."));

            long interactions = data.Current.Likes + data.Current.Comments + data.Current.Shares;
            insights.Add(Insight("fa fa-comments-o", "insight-item__icon--gold", "Active response",
                FormatNumber(interactions) + " likes, comments and shares came from this period."));

            insights.Add(Insight("fa fa-users", "insight-item__icon--blue", "Audience contribution",
                FormatSignedNumber(NetSubscribers(data.Current)) + " net subscribers were associated with video usage."));
            return insights;
        }

        private static InsightViewModel Insight(
            string icon,
            string accent,
            string title,
            string description)
        {
            return new InsightViewModel
            {
                IconCssClass = icon,
                AccentCssClass = accent,
                Title = title,
                Description = description
            };
        }

        private static MetricCardViewModel Metric(
            string label,
            string value,
            string tooltip,
            string icon,
            string accent,
            double current,
            double? previous)
        {
            string comparison = "Selected period";
            string comparisonCss = "is-neutral";
            if (previous.HasValue)
            {
                if (Math.Abs(previous.Value) < 0.0001)
                {
                    comparison = Math.Abs(current) < 0.0001 ? "No change" : "New activity";
                    comparisonCss = Math.Abs(current) < 0.0001 ? "is-neutral" : "is-positive";
                }
                else
                {
                    double change = (current - previous.Value) * 100d / Math.Abs(previous.Value);
                    comparison = change.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + "% vs previous";
                    comparisonCss = change > 0.05 ? "is-positive" : change < -0.05 ? "is-negative" : "is-neutral";
                }
            }

            return new MetricCardViewModel
            {
                Label = label,
                Value = value,
                Tooltip = tooltip,
                IconCssClass = icon,
                AccentCssClass = accent,
                ComparisonText = comparison,
                ComparisonCssClass = comparisonCss
            };
        }

        private static double? Previous(AnalyticsSummary summary, Func<AnalyticsSummary, double> selector)
        {
            return summary == null ? (double?)null : selector(summary);
        }

        private static long NetSubscribers(AnalyticsSummary summary)
        {
            return summary.SubscribersGained - summary.SubscribersLost;
        }

        private static double Rate(double numerator, double denominator)
        {
            return Math.Abs(denominator) < 0.0001 ? 0d : numerator * 100d / denominator;
        }

        private static string FormatNumber(long value)
        {
            double absolute = Math.Abs((double)value);
            if (absolute >= 1000000000d) return (value / 1000000000d).ToString("0.0", CultureInfo.InvariantCulture) + "B";
            if (absolute >= 1000000d) return (value / 1000000d).ToString("0.0", CultureInfo.InvariantCulture) + "M";
            if (absolute >= 1000d) return (value / 1000d).ToString("0.0", CultureInfo.InvariantCulture) + "K";
            return value.ToString("N0", CultureInfo.GetCultureInfo("en-IN"));
        }

        private static string FormatSignedNumber(long value)
        {
            return (value > 0 ? "+" : string.Empty) + FormatNumber(value);
        }

        private static string FormatHours(double minutes)
        {
            return (minutes / 60d).ToString("N1", CultureInfo.GetCultureInfo("en-IN")) + " hrs";
        }

        private static string FormatPercent(double value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        private static string FormatDuration(double totalSeconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(Math.Max(0d, totalSeconds));
            return duration.TotalHours >= 1d
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)duration.TotalHours, duration.Minutes, duration.Seconds)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", duration.Minutes, duration.Seconds);
        }

        private static string ShortLabel(string value)
        {
            string text = value ?? string.Empty;
            return text.Length <= 24 ? text : text.Substring(0, 21) + "...";
        }

        private static string TrafficSourceLabel(string key)
        {
            string label;
            if (TrafficSourceNames.TryGetValue(key ?? string.Empty, out label)) return label;
            return string.IsNullOrWhiteSpace(key)
                ? "Unknown source"
                : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('_', ' ').ToLowerInvariant());
        }

        private static string CountryLabel(string countryCode)
        {
            try
            {
                return new RegionInfo(countryCode).EnglishName;
            }
            catch (ArgumentException)
            {
                return string.IsNullOrWhiteSpace(countryCode) ? "Unknown market" : countryCode.ToUpperInvariant();
            }
        }
    }
}
