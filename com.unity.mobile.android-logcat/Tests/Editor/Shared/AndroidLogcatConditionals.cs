using System;
using UnityEditor;
using UnityEngine.TestTools;

namespace Unity.Android.Logcat
{
    [InitializeOnLoad]
    public class OnLoad
    {
        static OnLoad()
        {
            var runningOnYamato = Workspace.IsRunningOnYamato();
            var runningOnKatana = Workspace.IsRunningOnKatana();
            var runningOnBuildServer = Workspace.IsRunningOnBuildServer();
            var androidDeviceInfo = Workspace.GetAndroidDeviceInfo();
            var androidDeviceAvailable = Workspace.IsAndroidDeviceAvailable();

            Console.WriteLine($"Running On Yamato: {runningOnYamato}");
            Console.WriteLine($"Running On Katana: {runningOnKatana}");
            Console.WriteLine($"Running On Build Server: {runningOnBuildServer}");
            Console.WriteLine($"Android Device Info: {androidDeviceInfo}");
            Console.WriteLine($"Android Device Available: {androidDeviceAvailable}");

            // Ignore test only if running on Yamato or Katana and no device is available
            // We always want our test to run when running locally
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping(nameof(RequiresAndroidDeviceAttribute), runningOnBuildServer && !androidDeviceAvailable);
        }
    }

    public class RequiresAndroidDeviceAttribute : ConditionalIgnoreAttribute
    {
        public RequiresAndroidDeviceAttribute() :
            base(nameof(RequiresAndroidDeviceAttribute), "Not running since we don't have Android Device")
        { }
    }
}
