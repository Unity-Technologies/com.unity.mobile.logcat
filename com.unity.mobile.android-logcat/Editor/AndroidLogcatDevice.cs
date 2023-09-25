using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.Android;

namespace Unity.Android.Logcat
{
    internal abstract class IAndroidLogcatDevice
    {
        internal static Regex kNetworkDeviceRegex = new Regex(@"^.*:\d{1,5}$");

        private DeviceState m_State;
        internal enum DeviceConnectionType
        {
            USB,
            Network
        }

        internal enum DeviceState
        {
            Connected,
            Disconnected,
            Unauthorized,
            Unknown
        }

        // Check if it is Android 7 or above due to the below options are only available on these devices:
        // 1) '--pid'
        // 2) 'logcat -v year'
        // 3) '--regex'
        internal static readonly Version kAndroidVersion70 = new Version(7, 0);

        internal abstract int APILevel { get; }

        internal abstract string Manufacturer { get; }

        internal abstract string Model { get; }

        internal abstract Version OSVersion { get; }

        internal abstract string ABI { get; }

        internal abstract string Id { get; }

        internal abstract string DisplayName { get; }

        internal abstract string ShortDisplayName { get; }

        internal virtual void SendKeyAsync(AndroidLogcatDispatcher dispatcher, AndroidKeyCode keyCode, bool longPress) { }

        internal void SendKeyAsync(AndroidLogcatDispatcher dispatcher, AndroidKeyCode keyCode) { SendKeyAsync(dispatcher, keyCode, false); }

        internal virtual void SendTextAsync(AndroidLogcatDispatcher dispatcher, string text) { }

        internal virtual void StartOrResumePackage(string packageName, string activityName = null) { }

        internal virtual void StopPackage(string packageName) { }

        internal virtual void CrashPackage(string packageName) { }

        internal virtual void UninstallPackage(string packageName) { }

        internal virtual void KillProcess(string packageName, int processId, PosixSignal signal = PosixSignal.SIGNONE) { }

        internal bool SupportsFilteringByPid
        {
            get { return OSVersion >= kAndroidVersion70; }
        }

        internal bool SupportYearFormat
        {
            get { return OSVersion >= kAndroidVersion70; }
        }

        internal DeviceConnectionType ConnectionType
        {
            get
            {
                return kNetworkDeviceRegex.Match(Id).Success ? DeviceConnectionType.Network : DeviceConnectionType.USB;
            }
        }

        internal DeviceState State
        {
            get { return m_State; }
        }

        internal void UpdateState(DeviceState state)
        {
            m_State = state;
        }

        internal IAndroidLogcatDevice()
        {
            m_State = DeviceState.Unknown;
        }
    }

    internal class AndroidLogcatDevice : IAndroidLogcatDevice
    {
        private string m_Id;
        private AndroidBridge.AndroidDevice m_Device;
        private AndroidBridge.ADB m_ADB;
        private Version m_Version;
        private string m_DisplayName;


        internal AndroidLogcatDevice(AndroidBridge.ADB adb, string deviceId)
        {
            m_ADB = adb;
            m_Id = deviceId;

            if (adb == null)
            {
                m_Device = null;
                return;
            }

            try
            {
                m_Device = new AndroidBridge.AndroidDevice(adb, deviceId);
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log("Exception caugth while trying to retrieve device details for device {0}. This is harmless and device id will be used. Details\r\n:{1}", deviceId, ex);
                // device will be null in this case (and it will not be added to the cache)
                m_Device = null;
            }
        }

        internal override int APILevel
        {
            get
            {
                if (m_Device == null)
                    return 0;
                int value = 0;
                int.TryParse(m_Device.Properties["ro.build.version.sdk"], out value);
                return value;
            }
        }

        internal override string Manufacturer
        {
            get
            {
                if (m_Device == null)
                    return string.Empty;
                return m_Device.Properties["ro.product.manufacturer"];
            }
        }

        internal override string Model
        {
            get
            {
                if (m_Device == null)
                    return string.Empty;
                return m_Device.Properties["ro.product.model"];
            }
        }

        internal override Version OSVersion
        {
            get
            {
                if (m_Device == null)
                    return new Version();
                if (m_Version == null)
                {
                    var versionString = m_Device.Properties["ro.build.version.release"];
                    m_Version = AndroidLogcatUtilities.ParseVersion(versionString);
                }

                return m_Version;
            }
        }

        internal override string ABI
        {
            get
            {
                if (m_Device == null)
                    return string.Empty;
                return m_Device.Properties["ro.product.cpu.abi"];
            }
        }

        internal override string Id
        {
            get { return m_Id; }
        }

        internal override string DisplayName
        {
            get
            {
                if (m_Device == null || State != DeviceState.Connected)
                    return Id + " (" + State.ToString() + ")";
                else
                {
                    if (m_DisplayName != null)
                        return m_DisplayName;
                    m_DisplayName = $"{Manufacturer} {Model} (version: {OSVersion}, abi: {ABI}, sdk: {APILevel}, id: {Id})";
                    return m_DisplayName;
                }
            }
        }

        internal override string ShortDisplayName
        {
            get
            {
                var shortName = Manufacturer.Length > 0 ? $"{Manufacturer} {Model} ({Id})" : Id;
                if (m_Device == null || State != DeviceState.Connected)
                    return $"{shortName} ({State})";
                else
                    return shortName;
            }
        }

        /// <summary>
        /// Sends key to device, since it's a slow operation for some reason, we do it asynchronusly
        /// </summary>
        internal override void SendKeyAsync(AndroidLogcatDispatcher dispatcher, AndroidKeyCode keyCode, bool longPress)
        {
            dispatcher.Schedule(
                new AndroidLogcatTaskInput<AndroidBridge.ADB, string, AndroidKeyCode>()
                {
                    data1 = m_ADB,
                    data2 = Id,
                    data3 = keyCode
                },
                (input) =>
                {
                    var inputData = (AndroidLogcatTaskInput<AndroidBridge.ADB, string, AndroidKeyCode>)input;

                    var args = new List<string>(new[]
                    {
                        "-s",
                        inputData.data2,
                        "shell",
                        "input",
                        "keyevent"
                     });

                    if (longPress)
                        args.Add("--longpress");

                    args.Add(((int)inputData.data3).ToString());

                    AndroidLogcatInternalLog.Log($"adb {string.Join(" ", args)}");

                    inputData.data1.Run(args.ToArray(), $"Failed to send key event '{inputData.data3}'");
                    return null;
                },
            false);
        }


        internal static string[] SplitStringForSendText(string contents, string[] splits)
        {
            var pattern = string.Empty;
            foreach (var split in splits)
            {
                if (pattern.Length > 0)
                    pattern += "|";
                pattern += $"({Regex.Escape(split)})";
            }
            return Regex.Split(contents, pattern).Where(s => !string.IsNullOrEmpty(s)).ToArray();
        }

        /// <summary>
        /// Sends key to device, since it's a slow operation for some reason, we do it asynchronusly
        /// </summary>
        internal override void SendTextAsync(AndroidLogcatDispatcher dispatcher, string text)
        {
            dispatcher.Schedule(
                new AndroidLogcatTaskInput<AndroidBridge.ADB, string, string>()
                {
                    data1 = m_ADB,
                    data2 = Id,
                    data3 = text
                },
                (input) =>
                {
                    var inputData = (AndroidLogcatTaskInput<AndroidBridge.ADB, string, string>)input;

                    // It's tricky to send multiline string or lines containing %s (which translates into whitespace for adb), thus we split into separate lines
                    // And simulate enter key after each line or %s as separate events
                    // The following example must work correctly (send it as single multiline string):
                    // 'path:"C:\program files\Test"'
                    // %s
                    // ABC

                    var lines = SplitStringForSendText(inputData.data3.Replace("\r\n", "\n"), new[] { "%s", "\n" });

                    for (int i = 0; i < lines.Length; i++)
                    {

                        var formattedLine = lines[i];
                        if (formattedLine == "\n")
                        {
                            var args = new[]
{
                                "-s",
                                inputData.data2,
                                "shell",
                                "input",
                                "keyevent",
                                ((int)AndroidKeyCode.ENTER).ToString()
                            };

                            AndroidLogcatInternalLog.Log($"adb {string.Join(" ", args)}");

                            inputData.data1.Run(args, $"Failed to send key event 'Enter'");
                        }
                        else if (formattedLine == "%s")
                        {
                            var splits = new[] { "%", "s" };
                            foreach (var s in splits)
                            {
                                var args = new[]
                                {
                                    "-s",
                                    inputData.data2,
                                    "shell",
                                    "input",
                                    "text",
                                s
                                };

                                AndroidLogcatInternalLog.Log($"adb {string.Join(" ", args)}");
                                inputData.data1.Run(args, $"Failed to send key event 'Enter'");
                            }
                        }
                        else
                        {
                            // Note: Correctly escaping text for adb shell is tricky, we need to escape quotes
                            // Example which need to work:
                            // 'path:"C:\program files\Test"'
                            var toReplace = new KeyValuePair<string, string>[]
                            {
                                new KeyValuePair<string, string>("'", "'\\''"),
                                new KeyValuePair<string, string>("\"", "\\\""),
                                new KeyValuePair<string, string>(" ", "%s")
                            };

                            foreach (var rep in toReplace)
                            {
                                formattedLine = formattedLine.Replace(rep.Key, rep.Value);
                            }
                            formattedLine = $"'{formattedLine}'";

                            var args = new[]
                            {
                                "-s",
                                inputData.data2,
                                "shell",
                                "input",
                                "text",
                                formattedLine
                            };

                            AndroidLogcatInternalLog.Log($"adb {string.Join(" ", args)}");

                            inputData.data1.Run(args, $"Failed to send text '{formattedLine}'");
                        }
                    }

                    return null;
                },
            false);
        }

        internal override void StartOrResumePackage(string packageName, string activityName = null)
        {
            var args = new List<string>();
            args.AddRange(new[]
            {
                "-s",
                Id,
                "shell",
             });

            if (activityName == null)
            {
                args.AddRange(new[]
                {
                    "monkey",
                    $"-p {packageName}",
                    "-c android.intent.category.LAUNCHER 1"
                 });
            }
            else
            {
                args.AddRange(new[]
                {
                    "am",
                    "start",
                    $"-n \"{packageName}/{activityName}\""
                });
            }

            AndroidLogcatInternalLog.Log($"adb {string.Join(" ", args)}");

            m_ADB.Run(args.ToArray(), $"Failed to start package '{packageName}'");
        }

        internal override void StopPackage(string packageName)
        {
            var args = new[]
            {
                "-s",
                Id,
                "shell",
                "am",
                "force-stop",
                packageName
             };
            AndroidLogcatInternalLog.Log($"adb {string.Join(" ", args)}");

            m_ADB.Run(args, $"Failed to stop package '{packageName}'");
        }

        internal override void CrashPackage(string packageName)
        {
            var args = new[]
            {
                "-s",
                Id,
                "shell",
                "am",
                "crash",
                packageName
             };
            AndroidLogcatInternalLog.Log($"adb {string.Join(" ", args)}");

            m_ADB.Run(args, $"Failed to crash package '{packageName}'");
        }

        internal override void UninstallPackage(string packageName)
        {
            var args = new[]
{
                "-s",
                Id,
                "uninstall",
                packageName
             };
            AndroidLogcatInternalLog.Log($"adb {string.Join(" ", args)}");

            m_ADB.Run(args, $"Failed to uninstall package '{packageName}'");
        }


        internal override void KillProcess(string packageName, int processId, PosixSignal signal = PosixSignal.SIGNONE)
        {
            // Note: without run-as, you'll get Operation Not Permitted
            var args = new List<string>(
                new[]
                {
                    "-s",
                    Id,
                    "shell",
                    "run-as",
                    packageName,
                    "kill",
                 });

            if (signal > PosixSignal.SIGNONE)
                args.Add($"-s {(int)signal}");

            args.Add(processId.ToString());

            AndroidLogcatInternalLog.Log($"adb {string.Join(" ", args)}");

            m_ADB.Run(args.ToArray(), $"Failed to kill process '{processId}'");
        }
    }
}
