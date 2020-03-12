using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NUnit.Framework;

public class ValidationTests
{
    [Test]
    public void ProjectCanCompileWithoutErrorsOnNonAndroidPlatform()
    {
        Assert.IsTrue(EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android, "Active platform should be non Android");
    }
}
