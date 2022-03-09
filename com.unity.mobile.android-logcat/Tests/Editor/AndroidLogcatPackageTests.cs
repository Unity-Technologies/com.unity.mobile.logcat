using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Unity.Android.Logcat;
using UnityEngine;


internal class AndroidLogcatPackageTests : AndroidLogcatRuntimeTestBase
{
    [Test]
    public void DeadPackagesAreCleanupCorrectly()
    {
        try
        {
            InitRuntime();

            const int kPackagesToCreate = 10;
            var fakeDevice = new AndroidLogcatFakeDevice90("androiddevice0");
            for (int i = 0; i < kPackagesToCreate; i++)
            {
                var d = m_Runtime.UserSettings.CreatePackageInformation("com.unity.test" + i, i + 1, fakeDevice);
            }

            // All packages are alive, calling cleanup dead packages, shouldn't clean anything
            m_Runtime.UserSettings.CleanupDeadPackagesForDevice(fakeDevice, m_Runtime.Settings.MaxExitedPackagesToShow);
            var packages = m_Runtime.UserSettings.GetKnownPackages(fakeDevice);
            Assert.AreEqual(kPackagesToCreate, packages.Count);

            foreach (var p in packages)
                p.SetExited();

            m_Runtime.UserSettings.CleanupDeadPackagesForDevice(fakeDevice, m_Runtime.Settings.MaxExitedPackagesToShow);

            Assert.AreEqual(m_Runtime.Settings.MaxExitedPackagesToShow, packages.Count);

            // Check that recent packages are still there, only the old packages should be removed
            foreach (var p in packages)
                Assert.IsTrue(p.processId > 5);

            // Lower the number of max exited packages
            m_Runtime.Settings.MaxExitedPackagesToShow = 4;
            m_Runtime.UserSettings.CleanupDeadPackagesForDevice(fakeDevice, m_Runtime.Settings.MaxExitedPackagesToShow);
            packages = m_Runtime.UserSettings.GetKnownPackages(fakeDevice);
            Assert.AreEqual(m_Runtime.Settings.MaxExitedPackagesToShow, packages.Count);

            foreach (var p in packages)
                Assert.IsTrue(p.processId > 6);
        }
        finally
        {
            ShutdownRuntime();
        }
    }
}
