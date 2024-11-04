using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// UnityEditor.dll
namespace UnityEditor.Build
{
    public interface ILaunchProperties
    {
        // Not sure if need this
        public NamedBuildTarget BuildTarget { get; }
    }

    public interface IPostprocessLaunch : IOrderedCallback
    {
        void OnPostprocessLaunch(ILaunchProperties properties);
    }
}

// UnityEditor.Android.Extensions.dll
namespace UnityEditor.Android
{
    public class AndroidLaunchProperties : UnityEditor.Build.ILaunchProperties
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

// User code
class MyPostprocessLaunch : IPostprocessLaunch
{
    public int callbackOrder => 0;

    public void OnPostprocessLaunch(ILaunchProperties properties)
    {
        var a = properties.AsAndroidProperties();

        if (a != null)
        {
            // Do something with data. For ex., query process id
            ///var pid = ADB.GetInstance().Run($"get pid -s {a.DeviceId} {a.PackageName}/{a.ActivityName}");
        }
    }
}