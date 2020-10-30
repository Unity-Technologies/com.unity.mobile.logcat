using NUnit.Framework;
using System;
using Unity.Android.Logcat;

public class AndroidLogcatMemoryTests
{
    const int kKiloByte = 1000;

    // Produced by adb.exe shell dumpsys meminfo com.unity.TowerDefenceIncremental
    readonly string kPackageMemoryDump = @"
Applications Memory Usage(in Kilobytes) :
Uptime: 277764755 Realtime: 525450147

** MEMINFO in pid 21501 [com.unity.TowerDefenceIncremental] **
                   Pss  Private Private  SwapPss Heap     Heap Heap
                 Total Dirty    Clean Dirty     Size Alloc     Free
                ------   ------   ------   ------   ------   ------   ------
  Native Heap    25238    25172        0   124690   165624   157805     7818
  Dalvik Heap     1537      548      900      165     1680     1184      496
 Dalvik Other      243      224        0       14
        Stack       36       36        0       20
       Ashmem        4        0        0        0
      Gfx dev    57928    57800      128        0
    Other dev       66        0        8        0
     .so mmap     2070      468      216     1311
    .jar mmap      984        0        0        0
    .apk mmap      255        0        0        0
    .dex mmap       72       72        0       72
    .oat mmap       53        0        8        0
    .art mmap     3609     1928      532     2210
   Other mmap      220       72       16        1
   EGL mtrack    49956    49956        0        0
      Unknown     5402     5356        4    36518
        TOTAL   312674   141632     1812   165001   167304   158989     8314

 App Summary
                       Pss(KB)
                        ------
           Java Heap:     1234
         Native Heap:    43210
                Code:      764
               Stack:       36
            Graphics:   107884
       Private Other:     6580
              System:   169230

               TOTAL:   312674       TOTAL SWAP PSS:   165001

 Objects
               Views:        7         ViewRootImpl:        1
         AppContexts:        6           Activities:        1
              Assets:       16        AssetManagers:        0
       Local Binders:       13        Proxy Binders:       32
       Parcel memory:        6         Parcel count:       25
    Death Recipients:        0      OpenSSL Sockets:        0
            WebViews:        0

 SQL
         MEMORY_USED:        0
  PAGECACHE_OVERFLOW:        0          MALLOC_SIZE:        0
".Replace("\r", "");
    // Taken from Google Pixel 2 with Android 11
    readonly string kPackageMemoryDumpAndroid11 = @"
Applications Memory Usage (in Kilobytes):
Uptime: 502048736 Realtime: 502048736

** MEMINFO in pid 14978 [com.DefaultCompany.MemoryTests] **
                   Pss  Private  Private  SwapPss      Rss     Heap     Heap     Heap
                 Total    Dirty    Clean    Dirty    Total     Size    Alloc     Free
                ------   ------   ------   ------   ------   ------   ------   ------
  Native Heap   396846   396792        0        0   399380   550768   504803    40775
  Dalvik Heap     1003      888        0        0     5308    16146    12110     4036
 Dalvik Other     1146      776        0        0     2092
        Stack     1536     1536        0        0     1548
       Ashmem        4        0        0        0       12
      Gfx dev    35076    34948      128        0    35076
    Other dev      153       84       48        0      472
     .so mmap    48617     1828    44328        3    65940
    .jar mmap     2011        0      268        0    16112
    .apk mmap      154        0        0        0     6612
    .dex mmap      178      140       36        0      216
    .oat mmap       70        0       44        0      764
    .art mmap     9554     8844      420       12    19424
   Other mmap     1334        8     1324        0     1736
   EGL mtrack    49956    49956        0        0    49956
      Unknown   140908   140900        0        0   141484
        TOTAL   688561   636700    46596       15   688561   566914   516913    44811

 App Summary
                       Pss(KB)                        Rss(KB)
                        ------                         ------
           Java Heap:    10152                          24732
         Native Heap:   396792                         399380
                Code:    46672                          90480
               Stack:     1536                           1548
            Graphics:    85032                          85032
       Private Other:   143112
              System:     5265
             Unknown:                                  144960

           TOTAL PSS:   688561            TOTAL RSS:   746132       TOTAL SWAP PSS:       15

 Objects
               Views:        8         ViewRootImpl:        1
         AppContexts:        5           Activities:        1
              Assets:       12        AssetManagers:        0
       Local Binders:       16        Proxy Binders:       36
       Parcel memory:        7         Parcel count:       29
    Death Recipients:        0      OpenSSL Sockets:        0
            WebViews:        0

 SQL
         MEMORY_USED:        0
  PAGECACHE_OVERFLOW:        0          MALLOC_SIZE:        0
".Replace("\r", "");

    // adb.exe shell dumpsys meminfo sensors.qcom
    readonly string kSystemProcessDump = @"
Applications Memory Usage (in Kilobytes):
Uptime: 278194816 Realtime: 525880208
                   Pss  Private  Private  SwapPss     Heap     Heap     Heap
                 Total    Dirty    Clean    Dirty     Size    Alloc     Free
                ------   ------   ------   ------   ------   ------   ------
  Native Heap      156      156        0      156        0        2(6)        0
  Dalvik Heap        0        0        0        0        0        3(6)       0
        Stack        4        4        0       32
    Other dev        0        0        0        0
     .so mmap       80       60        0      320
   Other mmap      151       28      120       32
      Unknown      112      112        0      340
        TOTAL     1383      360      120      880        0        0        0

 App Summary
                       Pss(KB)
                        ------
           Java Heap:        1
         Native Heap:      156
                Code:       60
               Stack:        4
            Graphics:        2
       Private Other:      260
              System:      903

               TOTAL:     1383       TOTAL SWAP PSS:      880
";

    readonly string kProcessWithHugeValues = @"
Applications Memory Usage (in Kilobytes):
Uptime: 278194816 Realtime: 525880208
                   Pss  Private  Private  SwapPss     Heap     Heap     Heap
                 Total    Dirty    Clean    Dirty     Size    Alloc     Free
                ------   ------   ------   ------   ------   ------   ------
  Native Heap      156      156        0      156        0        {0}        0
  Dalvik Heap        0        0        0        0        0        0       0
        Stack        4        4        0       32
    Other dev        0        0        0        0
     .so mmap       80       60        0      320
   Other mmap      151       28      120       32
      Unknown      112      112        0      340
        TOTAL     1383      360      120      880        0        0        0

 App Summary
                       Pss(KB)
                        ------
           Java Heap:        1
         Native Heap:      156
                Code:       60
               Stack:        4
            Graphics:        2
       Private Other:      260
              System:      903

               TOTAL:     1383       TOTAL SWAP PSS:      880
".Replace("\r", "");

    [Test]
    public void CanParseMemoryDumpFromPackage([Values(true, false)] bool windowsEndLines)
    {
        var stats = new AndroidMemoryStatistics();
        if (windowsEndLines)
            stats.Parse(kPackageMemoryDump.Replace("\n", "\r\n"));
        else
            stats.Parse(kPackageMemoryDump);

        Assert.AreEqual(312674 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.Total));
        Assert.AreEqual(169230 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.System));
        Assert.AreEqual(43210 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.NativeHeap));
        Assert.AreEqual(107884 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.Graphics));
        Assert.AreEqual(36 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.Stack));
        Assert.AreEqual(1234 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.JavaHeap));

        Assert.AreEqual(0, stats.GetValue(MemoryGroup.ResidentSetSize, MemoryType.NativeHeap));
        Assert.AreEqual(0, stats.GetValue(MemoryGroup.ResidentSetSize, MemoryType.Total));

        Assert.AreEqual(157805 * kKiloByte, stats.GetValue(MemoryGroup.HeapAlloc, MemoryType.NativeHeap));
        Assert.AreEqual(1184 * kKiloByte, stats.GetValue(MemoryGroup.HeapAlloc, MemoryType.JavaHeap));
    }

    [Test]
    public void CanParseMemoryDumpFromPackageWithAndroid11([Values(true, false)] bool windowsEndLines)
    {
        var stats = new AndroidMemoryStatistics();

        if (windowsEndLines)
            stats.Parse(kPackageMemoryDumpAndroid11.Replace("\n", "\r\n"));
        else
            stats.Parse(kPackageMemoryDumpAndroid11);

        Assert.AreEqual(688561 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.Total));
        Assert.AreEqual(5265 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.System));
        Assert.AreEqual(396792 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.NativeHeap));
        Assert.AreEqual(85032 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.Graphics));
        Assert.AreEqual(1536 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.Stack));
        Assert.AreEqual(10152 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.JavaHeap));

        Assert.AreEqual(746132 * kKiloByte, stats.GetValue(MemoryGroup.ResidentSetSize, MemoryType.Total));
        Assert.AreEqual(0 * kKiloByte, stats.GetValue(MemoryGroup.ResidentSetSize, MemoryType.System));
        Assert.AreEqual(399380 * kKiloByte, stats.GetValue(MemoryGroup.ResidentSetSize, MemoryType.NativeHeap));
        Assert.AreEqual(85032 * kKiloByte, stats.GetValue(MemoryGroup.ResidentSetSize, MemoryType.Graphics));
        Assert.AreEqual(1548 * kKiloByte, stats.GetValue(MemoryGroup.ResidentSetSize, MemoryType.Stack));
        Assert.AreEqual(24732 * kKiloByte, stats.GetValue(MemoryGroup.ResidentSetSize, MemoryType.JavaHeap));

        Assert.AreEqual(504803 * kKiloByte, stats.GetValue(MemoryGroup.HeapAlloc, MemoryType.NativeHeap));
        Assert.AreEqual(12110 * kKiloByte, stats.GetValue(MemoryGroup.HeapAlloc, MemoryType.JavaHeap));
        Assert.AreEqual(550768 * kKiloByte, stats.GetValue(MemoryGroup.HeapSize, MemoryType.NativeHeap));
        Assert.AreEqual(16146 * kKiloByte, stats.GetValue(MemoryGroup.HeapSize, MemoryType.JavaHeap));
    }

    [Test]
    public void CanParseMemoryDumpFromProcess([Values(true, false)] bool windowsEndLines)
    {
        var stats = new AndroidMemoryStatistics();

        if (windowsEndLines)
            stats.Parse(kSystemProcessDump.Replace("\n", "\r\n"));
        else
            stats.Parse(kSystemProcessDump);

        Assert.AreEqual(1383 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.Total));
        Assert.AreEqual(903 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.System));
        Assert.AreEqual(156 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.NativeHeap));
        Assert.AreEqual(2 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.Graphics));
        Assert.AreEqual(4 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.Stack));
        Assert.AreEqual(1 * kKiloByte, stats.GetValue(MemoryGroup.ProportionalSetSize, MemoryType.JavaHeap));

        Assert.AreEqual(2 * kKiloByte, stats.GetValue(MemoryGroup.HeapAlloc, MemoryType.NativeHeap));
        Assert.AreEqual(3 * kKiloByte, stats.GetValue(MemoryGroup.HeapAlloc, MemoryType.JavaHeap));
        Assert.AreEqual(5 * kKiloByte, stats.GetValue(MemoryGroup.HeapAlloc, MemoryType.Total));
    }

    [Test]
    public void CanParseHugeValues([Values(true, false)] bool windowsEndLines)
    {
        const UInt64 kOneMegaByte = 1000;
        const UInt64 kOneGigabyte = 1000 * kOneMegaByte;
        const UInt64 kOneTerabyte = 1000 * kOneGigabyte;

        var inputs = new[] { kOneGigabyte, kOneTerabyte };
        foreach (var i in inputs)
        {
            // Note: Report contains values in kilobytes
            var contents = string.Format(kProcessWithHugeValues, i);

            var stats = new AndroidMemoryStatistics();
            if (windowsEndLines)
                stats.Parse(contents.Replace("\n", "\r\n"));
            else
                stats.Parse(contents);
            Assert.AreEqual(i * kKiloByte, stats.GetValue(MemoryGroup.HeapAlloc, MemoryType.NativeHeap));
        }
    }

    [Test]
    public void CanProvidePrettySizeText()
    {
        const UInt64 kOneMegaByte = 1000 * 1000;
        const UInt64 kOneGigabyte = 1000 * kOneMegaByte;
        const UInt64 kOneTerabyte = 1000 * kOneGigabyte;

        Assert.AreEqual("1000 KB", AndroidLogcatMemoryViewer.UInt64ToSizeString(kOneMegaByte));
        Assert.AreEqual("2 MB", AndroidLogcatMemoryViewer.UInt64ToSizeString(2 * kOneMegaByte));
        Assert.AreEqual("1000 MB", AndroidLogcatMemoryViewer.UInt64ToSizeString(kOneGigabyte));
        Assert.AreEqual("2 GB", AndroidLogcatMemoryViewer.UInt64ToSizeString(2 * kOneGigabyte));
        Assert.AreEqual("1000 GB", AndroidLogcatMemoryViewer.UInt64ToSizeString(kOneTerabyte));
        Assert.AreEqual("2 TB", AndroidLogcatMemoryViewer.UInt64ToSizeString(2 * kOneTerabyte));
    }
}
