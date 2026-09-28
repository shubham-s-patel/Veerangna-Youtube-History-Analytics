using System;
using System.Collections.Generic;
using System.Globalization;

namespace Veerangana.YouTubeAnalytics.Helpers
{
    public static class DateRangeHelper
    {
        private static readonly ISet<string> ValidKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "7d",
            "28d",
            "90d",
            "365d",
            "lifetime",
            "custom"
        };

        public static string NormalizeKey(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && ValidKeys.Contains(value)
                ? value.ToLowerInvariant()
                : "28d";
        }

        public static DateTime? ParseIsoDate(string value)
        {
            DateTime date;
            return DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date)
                ? date
                : (DateTime?)null;
        }
    }
}
