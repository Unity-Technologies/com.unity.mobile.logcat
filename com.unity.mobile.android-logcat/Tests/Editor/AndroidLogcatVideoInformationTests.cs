using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Android.Logcat;

/// <summary>Frame times from minimal hand-built MP4s, including broken ones.</summary>
class AndroidLogcatVideoInformationTests
{
    string m_Path;

    [SetUp]
    public void SetUp()
    {
        m_Path = Path.Combine(Path.GetTempPath(), $"{nameof(AndroidLogcatVideoInformationTests)}_{Guid.NewGuid():N}.mp4");
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(m_Path);
    }

    /// <summary>Proves the built files are valid, so the rejections below mean something.</summary>
    [Test]
    public void ReadsABuiltTable()
    {
        File.WriteAllBytes(m_Path, Mp4(1, 3, 100));
        var times = AndroidLogcatVideoInformation.ReadFrameTimes(m_Path);

        Assert.IsNotNull(times);
        CollectionAssert.AreEqual(new[] { 0.0, 0.1, 0.2 }, times);
    }

    [Test]
    public void RejectsATableClaimingTooManyFrames()
    {
        File.WriteAllBytes(m_Path, Mp4(1, uint.MaxValue, 1));
        Assert.IsNull(AndroidLogcatVideoInformation.ReadFrameTimes(m_Path));

        File.WriteAllBytes(m_Path, Mp4(1, AndroidLogcatVideoInformation.kMaxFrames + 1, 1));
        Assert.IsNull(AndroidLogcatVideoInformation.ReadFrameTimes(m_Path));
    }

    [Test]
    public void RejectsATableClaimingMoreEntriesThanItHolds()
    {
        File.WriteAllBytes(m_Path, Mp4(uint.MaxValue, 3, 100));
        Assert.IsNull(AndroidLogcatVideoInformation.ReadFrameTimes(m_Path));
    }

    [Test]
    public void RejectsABoxClaimingMoreThanTheFile()
    {
        var mp4 = Mp4(1, 3, 100);
        // The moov box's size.
        BigEndian(uint.MaxValue).CopyTo(mp4, 0);
        File.WriteAllBytes(m_Path, mp4);
        Assert.IsNull(AndroidLogcatVideoInformation.ReadFrameTimes(m_Path));
    }

    /// <summary>One video track at 1000 ticks a second; its table claims <paramref name="entryCount"/> entries.</summary>
    static byte[] Mp4(uint entryCount, params uint[] entries)
    {
        var handler = Box("hdlr", UInt32s(0, 0), Encoding.ASCII.GetBytes("vide"));
        // Version and flags, creation and modification times, timescale, duration.
        var header = Box("mdhd", UInt32s(0, 0, 0, 1000, 0));
        var stts = Box("stts", UInt32s(0, entryCount), UInt32s(entries));
        var table = Box("minf", Box("stbl", stts));
        return Box("moov", Box("trak", Box("mdia", handler, header, table)));
    }

    static byte[] Box(string type, params byte[][] content)
    {
        var body = content.SelectMany(c => c).ToArray();
        return BigEndian((uint)(8 + body.Length)).Concat(Encoding.ASCII.GetBytes(type)).Concat(body).ToArray();
    }

    static byte[] UInt32s(params uint[] values)
    {
        return values.SelectMany(BigEndian).ToArray();
    }

    static byte[] BigEndian(uint value)
    {
        return new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };
    }
}
