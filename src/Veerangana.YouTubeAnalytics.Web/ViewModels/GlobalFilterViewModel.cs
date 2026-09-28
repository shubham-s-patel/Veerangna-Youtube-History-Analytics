using System;
using System.Collections.Generic;

namespace Veerangana.YouTubeAnalytics.ViewModels
{
    public sealed class GlobalFilterViewModel
    {
        public GlobalFilterViewModel()
        {
            DateRanges = new List<DateRangeOptionViewModel>();
            ContentTypes = new List<FilterOptionViewModel>();
        }

        public string DateRangeKey { get; set; }

        public DateTime? CustomStartDate { get; set; }

        public DateTime? CustomEndDate { get; set; }

        public bool ComparePeriod { get; set; }

        public string ContentType { get; set; }

        public string Country { get; set; }

        public IList<DateRangeOptionViewModel> DateRanges { get; private set; }

        public IList<FilterOptionViewModel> ContentTypes { get; private set; }
    }
}
