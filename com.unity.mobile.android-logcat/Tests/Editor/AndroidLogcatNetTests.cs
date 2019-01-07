using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Unity.Android.Logcat;

public class AndroidLogcatNetTests
{ 
    // Ensure we're running tests with .NET 3.5, because Unity 2018.3 and older don't have .NET 3.5 deprecated
    [Test]
    public void EnsureNET35IsUsed()
    {
#if !NET_2_0 && !UNITY_2019_3_OR_NEWER
        Assert.Fail("Tests project should be using .NET 3.5, did you modify Scripting Runtime Version?");
#endif
    }
}
