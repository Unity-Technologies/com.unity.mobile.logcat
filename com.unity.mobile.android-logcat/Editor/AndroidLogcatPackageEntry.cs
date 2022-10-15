using System;

namespace Unity.Android.Logcat
{
    [Serializable]
    internal class PackageEntry
    {
        public string Name { set; get; }
        public string Installer { set; get; }
        public string UID { set; get; }

        internal int GetId() => GetHashCode();

        internal void Launch()
        {
            var adb = AndroidLogcatManager.instance.Runtime.Tools.ADB;
            var device = AndroidLogcatManager.instance.Runtime.DeviceQuery.SelectedDevice;
            // Not using 'shell am start' since it requires knowing the activity name
            var cmd = $"-s {device.Id} shell monkey -p {Name} -c android.intent.category.LAUNCHER 1";
            var output = adb.Run(new[] { cmd }, "Unable to launch package");
            UnityEngine.Debug.Log(output);
        }
        internal void Pause()
        {
            var adb = AndroidLogcatManager.instance.Runtime.Tools.ADB;
            var device = AndroidLogcatManager.instance.Runtime.DeviceQuery.SelectedDevice;
            var cmd = $"-s {device.Id} shell input keyevent 3";
            var output = adb.Run(new[] { cmd }, "Unable to pause the package");
            UnityEngine.Debug.Log(output);
        }

        internal void Stop()
        {
            var adb = AndroidLogcatManager.instance.Runtime.Tools.ADB;
            var device = AndroidLogcatManager.instance.Runtime.DeviceQuery.SelectedDevice;
            var cmd = $"-s {device.Id} shell am force-stop {Name}";
            var output = adb.Run(new[] { cmd }, "Unable to stop package");
            UnityEngine.Debug.Log(output);
        }

        internal void Uninstall()
        {
            var adb = AndroidLogcatManager.instance.Runtime.Tools.ADB;
            var device = AndroidLogcatManager.instance.Runtime.DeviceQuery.SelectedDevice;
            var cmd = $"-s {device.Id} uninstall {Name}";
            var output = adb.Run(new[] { cmd }, "Unable to uninstall the package");
            UnityEngine.Debug.Log(output);
        }
    }
}
