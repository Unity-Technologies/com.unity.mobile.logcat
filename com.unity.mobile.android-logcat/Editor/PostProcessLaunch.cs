using System.Diagnostics;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Windows;

// UnityEditor.dll
namespace UnityEditor.Build
{
    public interface ILaunchProperties
    {
        // Not sure if need this
        public NamedBuildTarget BuildTarget { get; }
    }

    // Platforms which don't implement their own launch properties will return DefaultLaunchProperties instance
    public class DefaultLaunchProperties : UnityEditor.Build.ILaunchProperties
    {
        public virtual NamedBuildTarget BuildTarget => NamedBuildTarget.Unknown;
    }

    public interface IPostprocessLaunch : IOrderedCallback
    {
        // On some platforms like Android, you can launch on multiple devices at once
        void OnPostprocessLaunch(ILaunchProperties[] launchProperties);
    }
}

// UnityEditor.Android.Extensions.dll
namespace UnityEditor.Android
{
    public class AndroidLaunchProperties : DefaultLaunchProperties
    {
        public string DeviceId { get; }
        public string PackageName { get; }
        public string ActivityName { get; }
        public NamedBuildTarget BuildTarget => NamedBuildTarget.Android;

        internal AndroidLaunchProperties(string deviceId, string packageName, string activityName)
        {
            DeviceId = deviceId;
            PackageName = packageName;
            ActivityName = activityName;
        }
    }

    public static class AndroidLaunchPropertiesExtensions
    {
        public static AndroidLaunchProperties AsAndroidProperties(this ILaunchProperties properties)
        {
            return properties as AndroidLaunchProperties;
        }
    }
}

// UnityEditor.Windows.Extensions.dll
namespace UnityEditor.Windows
{
    public class WindowsStandaloneLaunchProperties : DefaultLaunchProperties
    {
        public string ExecutablePath { get; }
        public NamedBuildTarget BuildTarget => NamedBuildTarget.Standalone;

        internal WindowsStandaloneLaunchProperties(string executablePath)
        {
            ExecutablePath = executablePath;
        }
    }

    public static class WindowsLaunchPropertiesExtensions
    {
        public static WindowsStandaloneLaunchProperties AsWindowsStandaloneProperties(this ILaunchProperties properties)
        {
            return properties as WindowsStandaloneLaunchProperties;
        }
    }
}

// UnityEditor.OSX.Extensions.dll
namespace UnityEditor.OSX
{
    public class MacOsStandaloneLaunchProperties : DefaultLaunchProperties
    {
        public string BundlePath { get; }
        public NamedBuildTarget BuildTarget => NamedBuildTarget.Standalone;

        internal MacOsStandaloneLaunchProperties(string bundlePath)
        {
            BundlePath = bundlePath;
        }
    }

    public static class WindowsLaunchPropertiesExtensions
    {
        public static MacOsStandaloneLaunchProperties AsMacOsStandaloneProperties(this ILaunchProperties properties)
        {
            return properties as MacOsStandaloneLaunchProperties;
        }
    }
}

// User code
class MyPostprocessLaunch : IPostprocessLaunch
{
    public int callbackOrder => 0;

    public void OnPostprocessLaunch(ILaunchProperties[] properties)
    {
        foreach (ILaunchProperties p in properties)
        {
            var androidLaunchProperties = p.AsAndroidProperties();

            if (androidLaunchProperties != null)
            {
                // Do something with data. For ex., query process id
                ///var pid = ADB.GetInstance().Run($"get pid -s {a.DeviceId} {a.PackageName}/{a.ActivityName}");
            }

            var windowsStandaloneLaunchProprties = p.AsWindowsStandaloneProperties();
            if (windowsStandaloneLaunchProprties != null)
            {
                // Do something with data. For ex., query process id
                ///var pid = Shell.Execute($"Get-Process {Path.GetFileName(windowsStandaloneLaunchProprties.ExecutablePath)}");
            }

            if (p.BuildTarget == NamedBuildTarget.PS4)
            {
                UnityEngine.Debug.Log("Unity launched something on PS4");
            }
        }
    }
}