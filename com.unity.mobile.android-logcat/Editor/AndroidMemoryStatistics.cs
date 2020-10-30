using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;


namespace Unity.Android.Logcat
{
    /// <summary>
    /// https://developer.android.com/studio/command-line/dumpsys#meminfo
    /// </summary>
    internal enum MemoryGroup
    {
        ResidentSetSize,
        ProportionalSetSize,
        HeapAlloc,
        HeapSize
    }

    internal enum MemoryType
    {
        Unknown = -1,
        NativeHeap = 0,
        JavaHeap = 1,
        Code = 2,
        Stack = 3,
        Graphics = 4,
        PrivateOther = 5,
        System = 6,
        Total = 7,
        TotalSwapPss = 8
    }


    internal class AndroidMemoryStatistics
    {
        const UInt64 kOneKiloByte = 1000;
        private MemoryGroup[] m_MemoryGroups = (MemoryGroup[])Enum.GetValues(typeof(MemoryGroup));
        private Dictionary<MemoryType, UInt64>[] m_Data = new Dictionary<MemoryType, UInt64>[Enum.GetValues(typeof(MemoryGroup)).Length];

        private Regex m_RssAvailable = new Regex(@"Pss\s+Private\s+Private\s+SwapPss\s+Rss\s+Heap\s+Heap\s+Heap", RegexOptions.IgnoreCase);
        private Regex m_PssOnlyData = new Regex(@"([\w\s]+):\s+(\d+).*", RegexOptions.IgnoreCase);
        private Regex m_PssAndRssData = new Regex(@"([\w\s]+):\s+(\d+)\s*(\d*)\n", RegexOptions.IgnoreCase);
        private Regex m_PssAndRssTotal = new Regex(@"\s+TOTAL PSS:\s+(\d+)\s+TOTAL RSS:\s+(\d+).*", RegexOptions.IgnoreCase);

        private Dictionary<MemoryType, UInt64> GetPSSMemoryGroup()
        {
            return m_Data[(int)MemoryGroup.ProportionalSetSize];
        }

        private Dictionary<MemoryType, UInt64> GetRSSMemoryGroup()
        {
            return m_Data[(int)MemoryGroup.ResidentSetSize];
        }

        private Dictionary<MemoryType, UInt64> GetHeapAllocGroup()
        {
            return m_Data[(int)MemoryGroup.HeapAlloc];
        }

        private MemoryType NameToMemoryType(string name)
        {
            name = name.ToLower();
            if (name.Equals("native heap"))
                return MemoryType.NativeHeap;
            if (name.Equals("java heap"))
                return MemoryType.JavaHeap;
            if (name.Equals("code"))
                return MemoryType.Code;
            if (name.Equals("stack"))
                return MemoryType.Stack;
            if (name.Equals("graphics"))
                return MemoryType.Graphics;
            if (name.Equals("private other"))
                return MemoryType.PrivateOther;
            if (name.Equals("system"))
                return MemoryType.System;
            if (name.Equals("total"))
                return MemoryType.Total;
            if (name.Equals("total swap pss"))
                return MemoryType.TotalSwapPss;
            return MemoryType.Unknown;
        }

        internal AndroidMemoryStatistics()
        {
            foreach (var g in m_MemoryGroups)
            {
                m_Data[(int)g] = new Dictionary<MemoryType, UInt64>();
            }
        }

        internal void Clear()
        {
            foreach (var g in m_MemoryGroups)
            {
                m_Data[(int)g].Clear();
            }
        }

        private void ParseAppSummaryData(Match match, int groupId, Dictionary<MemoryType, UInt64> data)
        {
            var name = match.Groups[1].Value.Trim().ToLower();
            var value = match.Groups[groupId].Value;
            var sizeInKBytes = string.IsNullOrEmpty(value) ? 0 : UInt64.Parse(value);
            MemoryType type = NameToMemoryType(name);
            if (type != MemoryType.Unknown)
                data[type] = sizeInKBytes * kOneKiloByte;
        }

        private void ParseAppSummary(string appSummary)
        {
            Dictionary<MemoryType, UInt64> pssData = GetPSSMemoryGroup();
            Dictionary<MemoryType, UInt64> rssData = GetRSSMemoryGroup();

            bool rssDataAvailable = appSummary.Contains("Rss(KB)");
            MatchCollection matches;
            if (rssDataAvailable)
            {
                matches = m_PssAndRssData.Matches(appSummary);
                foreach (Match match in matches)
                {
                    ParseAppSummaryData(match, 2, pssData);
                    ParseAppSummaryData(match, 3, rssData);
                }

                var totalMemoryMatch = m_PssAndRssTotal.Match(appSummary);
                if (!totalMemoryMatch.Success)
                    throw new Exception("Failed to find total pss and rss size in\n" + appSummary);

                pssData[MemoryType.Total] = UInt64.Parse(totalMemoryMatch.Groups[1].Value) * kOneKiloByte;
                rssData[MemoryType.Total] = UInt64.Parse(totalMemoryMatch.Groups[2].Value) * kOneKiloByte;
            }
            else
            {
                matches = m_PssOnlyData.Matches(appSummary);
                foreach (Match match in matches)
                    ParseAppSummaryData(match, 2, pssData);
            }

            UInt64 dummy;
            if (!pssData.TryGetValue(MemoryType.NativeHeap, out dummy))
            {
                throw new Exception("Failed to find pss native heap size in\n" + appSummary);
            }
        }

        /// <summary>
        /// Some number values are in format like this 3(6)
        /// We need to convert those to 3
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private string FixNumberValue(string value)
        {
            int index = value.IndexOf("(");
            return index == -1 ? value : value.Substring(0, index);
        }

        internal void ParseHeapInformation(string heapInformation)
        {
            string postFix;
            if (m_RssAvailable.Match(heapInformation).Success)
                postFix = @"\s+\S+\s+\S+\s+\S+\s+\S+\s+(?<rssSize>\S+)\s+(?<heapSize>\S+)\s+(?<heapAlloc>\S+)\s+\S+";
            else
                postFix = @"\s+\S+\s+\S+\s+\S+\s+\S+\s+(?<heapSize>\S+)\s+(?<heapAlloc>\S+)\s+\S+";

            Regex native = new Regex("Native Heap" + postFix, RegexOptions.IgnoreCase);
            Regex java = new Regex("Dalvik Heap" + postFix, RegexOptions.IgnoreCase);

            var regexes = new[] { native, java };
            var types = new[] { MemoryType.NativeHeap, MemoryType.JavaHeap };

            var totalHeapAlloc = (UInt64)0;
            var totalHeapSize = (UInt64)0;
            for (int i = 0; i < regexes.Length; i++)
            {
                var match = regexes[i].Match(heapInformation);
                if (match.Success)
                {
                    var value = UInt64.Parse(FixNumberValue(match.Groups["heapAlloc"].Value)) * kOneKiloByte;
                    SetValue(MemoryGroup.HeapAlloc, types[i], value);
                    totalHeapAlloc += value;

                    value = UInt64.Parse(FixNumberValue(match.Groups["heapSize"].Value)) * kOneKiloByte;
                    SetValue(MemoryGroup.HeapSize, types[i], value);
                    totalHeapSize += value;
                }
            }

            SetValue(MemoryGroup.HeapAlloc, MemoryType.Total, totalHeapAlloc);
            SetValue(MemoryGroup.HeapSize, MemoryType.Total, totalHeapSize);
        }

        internal UInt64 GetValue(MemoryGroup group, MemoryType type)
        {
            UInt64 value;
            if (m_Data[(int)group].TryGetValue(type, out value))
                return value;
            return 0;
        }

        internal void SetValue(MemoryGroup group, MemoryType type, UInt64 value)
        {
            m_Data[(int)group][type] = value;
        }

        /// <summary>
        /// Parses contents from command 'adb shell dumpsys meminfo package_name'
        /// </summary>
        /// <param name="contents"></param>
        /// <returns></returns>
        internal void Parse(string contents)
        {
            int appSummary = contents.IndexOf("App Summary");
            if (appSummary == -1)
                throw new Exception("Failed to find App Summary:\n" + contents);
            contents = contents.Replace("\r", "");
            ParseHeapInformation(contents.Substring(0, appSummary));
            ParseAppSummary(contents.Substring(appSummary));
        }

        internal void SetPSSFakeData(UInt64 totalMemory, UInt64 nativeHeap)
        {
            SetValue(MemoryGroup.ProportionalSetSize, MemoryType.Total, totalMemory);
            SetValue(MemoryGroup.ProportionalSetSize, MemoryType.NativeHeap, nativeHeap);
        }

        internal void SetHeapAllocData(UInt64 totalMemory, UInt64 nativeHeap)
        {
            SetValue(MemoryGroup.HeapAlloc, MemoryType.Total, totalMemory);
            SetValue(MemoryGroup.HeapAlloc, MemoryType.NativeHeap, nativeHeap);
        }

        internal void SetHeapSizeData(UInt64 totalMemory, UInt64 nativeHeap)
        {
            SetValue(MemoryGroup.HeapSize, MemoryType.Total, totalMemory);
            SetValue(MemoryGroup.HeapSize, MemoryType.NativeHeap, nativeHeap);
        }
    }
}
