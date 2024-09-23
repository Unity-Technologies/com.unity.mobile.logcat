using System.Collections.Generic;
using NUnit.Framework;
using Unity.Android.Logcat;

class AndroidLogcatQueryLayoutTests
{
    [Test]
    public void CanParseSimpleLayout()
    {
        var contents = @"<?xml version='1.0' encoding='UTF-8' standalone='yes' ?>
<hierarchy rotation=""0"">
    <node index=""0"" resource-id=""test"" class=""android.widget.FrameLayout"" bounds=""[1,2][3,4]"">
        <node index=""0"" resource-id=""test2"" class=""android.widget.View"" bounds=""[5,6][7,8]""/>
    </node>
</hierarchy>
";
        var nodes = new List<AndroidLogcatQueryLayout.LayoutNode>();
        AndroidLogcatQueryLayout.ParseNodes(nodes, contents);

        Assert.AreEqual(1, nodes.Count);
        StringAssert.AreEqualIgnoringCase("android.widget.FrameLayout", nodes[0].ClassName);
        StringAssert.AreEqualIgnoringCase("test", nodes[0].ResourceId);
        Assert.AreEqual(1, nodes[0].Bounds.x);
        Assert.AreEqual(2, nodes[0].Bounds.y);
        Assert.AreEqual(3, nodes[0].Bounds.xMax);
        Assert.AreEqual(4, nodes[0].Bounds.yMax);
        Assert.AreEqual(0, nodes[0].Id);

        var childs = nodes[0].Childs;
        Assert.AreEqual(1, childs.Count);

        StringAssert.AreEqualIgnoringCase("android.widget.View", childs[0].ClassName);
        StringAssert.AreEqualIgnoringCase("test2", childs[0].ResourceId);
        Assert.AreEqual(5, childs[0].Bounds.x);
        Assert.AreEqual(6, childs[0].Bounds.y);
        Assert.AreEqual(7, childs[0].Bounds.xMax);
        Assert.AreEqual(8, childs[0].Bounds.yMax);
        Assert.AreEqual(1, childs[0].Id);
    }

    [Test]
    public void CanParseEmptyLayout()
    {
        var nodes = new List<AndroidLogcatQueryLayout.LayoutNode>();
        AndroidLogcatQueryLayout.ParseNodes(nodes, string.Empty);
        Assert.AreEqual(0, nodes.Count);
    }
}
