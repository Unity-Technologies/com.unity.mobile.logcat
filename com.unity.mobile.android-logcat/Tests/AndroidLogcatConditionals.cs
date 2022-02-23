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
            var androidDeviceInfo= Workspace.GetAndroidDeviceInfoAvailable();

            Console.WriteLine($"Running On Yamato: {runningOnYamato}");
            Console.WriteLine($"Android Device Info: {androidDeviceInfo}");

            if (!string.IsNullOrEmpty(androidDeviceInfo))
            {
                Console.WriteLine($"Connecting to Android Device");
                var result = UnityEditor.Android.ADB.GetInstance().Run(new[]
                {
                    "connect",
                    androidDeviceInfo
                },
                $"Failed to connect to '{androidDeviceInfo}'");

                Console.WriteLine($"Result:\n{result}");
            }

            // Ignore test only if running on Yamato and device info is not available
            // We always want our test to run when running locally
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping(nameof(RequiresAndroidDeviceAttribute), runningOnYamato && !string.IsNullOrEmpty(androidDeviceInfo));
        }
    }

    public class RequiresAndroidDeviceAttribute : ConditionalIgnoreAttribute
    {
        public RequiresAndroidDeviceAttribute() :
            base(nameof(RequiresAndroidDeviceAttribute), "Not running since we don't have Android Device")
        { }
    }
}
