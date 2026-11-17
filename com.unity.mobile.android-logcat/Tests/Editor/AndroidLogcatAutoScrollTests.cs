using NUnit.Framework;
using Unity.Android.Logcat;

class AndroidLogcatAutoScrollTests
{
    const int kEntryCount = 1000;

    // Mirrors GUI.BeginScrollView: the content height includes the extra empty rows below the last entry,
    // and the viewport is reduced by the always visible horizontal scrollbar
    static float GetMaxScrollY(float visibleHeight, int entryCount, int rowHeight, float horizontalScrollbarHeight)
    {
        var totalHeight = rowHeight * (entryCount + AndroidLogcatConsoleWindow.kExtraMessageCount);
        var viewportHeight = visibleHeight - horizontalScrollbarHeight;
        return totalHeight - viewportHeight;
    }

    [Test]
    public void LastEntryIsVisibleWhenScrolledToBottom_ForAnyWindowHeight(
        [Values(12, 16, 20, 25, 30)] int rowHeight,
        [Values(0.0f, 13.0f, 15.0f)] float horizontalScrollbarHeight)
    {
        // Previously comparing rounded item counts failed when the fractional part of visible rows
        // was bigger than horizontal scrollbar height, for ex., 350px height with 16px rows
        for (float visibleHeight = 100.0f; visibleHeight <= 1500.0f; visibleHeight += 0.5f)
        {
            var maxScrollY = GetMaxScrollY(visibleHeight, kEntryCount, rowHeight, horizontalScrollbarHeight);
            Assert.IsTrue(AndroidLogcatConsoleWindow.IsLastEntryVisible(maxScrollY, visibleHeight, kEntryCount, rowHeight),
                $"Last entry should be visible at bottom, visible height {visibleHeight}, row height {rowHeight}, scrollbar height {horizontalScrollbarHeight}");
        }
    }

    [Test]
    public void LastEntryIsNotVisibleWhenScrolledAboveIt()
    {
        const int rowHeight = 16;
        const float visibleHeight = 350.0f;
        var lastEntryBottom = rowHeight * kEntryCount;

        Assert.IsFalse(AndroidLogcatConsoleWindow.IsLastEntryVisible(0.0f, visibleHeight, kEntryCount, rowHeight));
        Assert.IsFalse(AndroidLogcatConsoleWindow.IsLastEntryVisible(lastEntryBottom - visibleHeight - 1.0f, visibleHeight, kEntryCount, rowHeight));
        Assert.IsTrue(AndroidLogcatConsoleWindow.IsLastEntryVisible(lastEntryBottom - visibleHeight, visibleHeight, kEntryCount, rowHeight));
    }

    [Test]
    public void LastEntryIsVisibleWhenAllEntriesFit()
    {
        const int rowHeight = 16;
        Assert.IsTrue(AndroidLogcatConsoleWindow.IsLastEntryVisible(0.0f, 350.0f, 10, rowHeight));
        Assert.IsTrue(AndroidLogcatConsoleWindow.IsLastEntryVisible(0.0f, 350.0f, 0, rowHeight));
    }
}
