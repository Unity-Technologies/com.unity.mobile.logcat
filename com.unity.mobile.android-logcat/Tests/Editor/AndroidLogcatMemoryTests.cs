using NUnit.Framework;
using Unity.Android.Logcat;

public class AndroidLogcatMemoryTests
{
    // Produced by adb.exe shell dumpsys meminfo com.unity.TowerDefenceIncremental
    const string kPackageMemoryDump = @"
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
";

    // adb.exe shell dumpsys meminfo sensors.qcom
    const string kSystemProcessDump = @"
Applications Memory Usage (in Kilobytes):
Uptime: 278194816 Realtime: 525880208
                   Pss  Private  Private  SwapPss     Heap     Heap     Heap
                 Total    Dirty    Clean    Dirty     Size    Alloc     Free
                ------   ------   ------   ------   ------   ------   ------
  Native Heap      156      156        0      156        0        0        0
  Dalvik Heap        0        0        0        0        0        0        0
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

    [Test]
    public void CanParseMemoryDumpFromPackage()
    {
        var stats = new AndroidLogcatMemoryViewer.AndroidMemoryStatistics();
        stats.Parse(kPackageMemoryDump);

        const int kKiloBytes = 1024;

        Assert.AreEqual(312674 * kKiloBytes, stats.Total);
        Assert.AreEqual(169230 * kKiloBytes, stats.System);
        Assert.AreEqual(43210 * kKiloBytes, stats.NativeHeap);
        Assert.AreEqual(107884 * kKiloBytes, stats.Graphics);
        Assert.AreEqual(36 * kKiloBytes, stats.Stack);
        Assert.AreEqual(1234 * kKiloBytes, stats.JavaHeap);
    }

    [Test]
    public void CanParseMemoryDumpFromProcess()
    {
        var stats = new AndroidLogcatMemoryViewer.AndroidMemoryStatistics();
        stats.Parse(kSystemProcessDump);

        const int kKiloBytes = 1024;

        Assert.AreEqual(1383 * kKiloBytes, stats.Total);
        Assert.AreEqual(903 * kKiloBytes, stats.System);
        Assert.AreEqual(156 * kKiloBytes, stats.NativeHeap);
        Assert.AreEqual(2 * kKiloBytes, stats.Graphics);
        Assert.AreEqual(4 * kKiloBytes, stats.Stack);
        Assert.AreEqual(1 * kKiloBytes, stats.JavaHeap);
    }
}
