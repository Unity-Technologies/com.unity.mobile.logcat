using NUnit.Framework;
using System;
using Unity.Android.Logcat;

public class AndroidLogcatFastListViewTests
{
    [Test]
    public void CanClearItems()
    {
        var view = new AndroidLogcatFastListView(null, 3);
        view.AddEntries(new[] { "a" });
        Assert.AreEqual(1, view.Entries.Count);

        view.ClearEntries();
        Assert.AreEqual(0, view.Entries.Count);
    }

    [Test]
    public void CanAppendItems()
    {
        var view = new AndroidLogcatFastListView(null, 3);

        view.AddEntries(new[] { "a" });
        Assert.AreEqual(1, view.Entries.Count);
        StringAssert.AreEqualIgnoringCase("a", view.Entries[0].Value);

        view.AddEntries(new[] { "b" });
        Assert.AreEqual(2, view.Entries.Count);
        StringAssert.AreEqualIgnoringCase("b", view.Entries[1].Value);

        view.AddEntries(new[] { "c" });
        Assert.AreEqual(3, view.Entries.Count);
        StringAssert.AreEqualIgnoringCase("c", view.Entries[2].Value);

        view.AddEntries(new[] { "d" });
        Assert.AreEqual(3, view.Entries.Count);
        StringAssert.AreEqualIgnoringCase("d", view.Entries[2].Value);

    }

    [Test]
    public void CanAppendItemsNotExceedingCapacity()
    {
        var view = new AndroidLogcatFastListView(null, 3);
        view.AddEntries(new[] { "a", "b" });
        Assert.AreEqual(2, view.Entries.Count);

        view.AddEntries(new[] { "c", "d" });
        Assert.AreEqual(3, view.Entries.Count);
        StringAssert.AreEqualIgnoringCase("b", view.Entries[0].Value);
        StringAssert.AreEqualIgnoringCase("c", view.Entries[1].Value);
        StringAssert.AreEqualIgnoringCase("d", view.Entries[2].Value);
    }

    [Test]
    public void CanAppendMoreItemsThanCapacity()
    {
        var view = new AndroidLogcatFastListView(null, 3);
        view.AddEntries(new[] { "a", "b", "c", "d" });
        Assert.AreEqual(3, view.Entries.Count);
        StringAssert.AreEqualIgnoringCase("b", view.Entries[0].Value);
        StringAssert.AreEqualIgnoringCase("c", view.Entries[1].Value);
        StringAssert.AreEqualIgnoringCase("d", view.Entries[2].Value);
    }

    [Test]
    public void CanAppendEmptyArray()
    {
        var view = new AndroidLogcatFastListView(null, 3);
        view.AddEntries(Array.Empty<string>());
        Assert.AreEqual(0, view.Entries.Count);
        view.AddEntries(new[] { "a", "b", "c", "d" });
        Assert.AreEqual(3, view.Entries.Count);
    }
}
