#if PLATFORM_ANDROID
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor.Android;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatUtilities
    {
        /// <summary>
        /// Capture the screenshot on the given device.
        /// </summary>
        /// <returns> Return the path to the screenshot on the PC. </returns>
        public static string CaptureScreen(ADB adb, string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return null;

            try
            {
                const string screenshotPathOnDevice = "/sdcard/screen.png";

                // Capture the screen on the device.
                var cmd = string.Format("-s {0} shell screencap {1}", deviceId, screenshotPathOnDevice);
                AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);

                var errorMsg = "Unable to capture the screen for device ";
                var outputMsg = adb.Run(new[] { cmd }, errorMsg + deviceId);
                if (outputMsg.StartsWith(errorMsg))
                {
                    AndroidLogcatInternalLog.Log(outputMsg);
                    Debug.LogError(outputMsg);
                    return null;
                }

                // Pull screenshot from the device to temp folder.
                var filePath = Path.Combine(Path.GetTempPath(), "screen_" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".png");
                cmd = string.Format("-s {0} pull {1} {2}", deviceId, screenshotPathOnDevice, filePath);
                AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);

                errorMsg = "Unable to pull the screenshot from device ";
                outputMsg = adb.Run(new[] { cmd }, errorMsg + deviceId);
                if (outputMsg.StartsWith(errorMsg))
                {
                    AndroidLogcatInternalLog.Log(outputMsg);
                    Debug.LogError(outputMsg);
                    return null;
                }

                return filePath;
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log("Exception caugth while capturing screen on device {0}. Details\r\n:{1}", deviceId, ex);
                return null;
            }
        }

        /// <summary>
        /// Get the top activity on the given device.
        /// </summary>
        public static bool GetTopActivityInfo(ADB adb, string deviceId, ref string packageName, ref int packagePid)
        {
            if (string.IsNullOrEmpty(deviceId))
                return false;
            try
            {
                var cmd = "-s " + deviceId + " shell \"dumpsys activity\" ";
                AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);
                var output = adb.Run(new[] { cmd }, "Unable to get the top activity.");
                packagePid = AndroidLogcatUtilities.ParseTopActivityPackageInfo(output, out packageName);
                return packagePid != -1;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Return the pid of the given package on the given device.
        /// </summary>
        public static int GetPidFromPackageName(ADB adb, AndroidLogcatDevice device, string deviceId, string packageName)
        {
            if (string.IsNullOrEmpty(deviceId))
                return -1;

            try
            {
                string cmd = null;
                if (device.SupportsFilteringByPid)
                    cmd = string.Format("-s {0} shell pidof -s {1}", deviceId, packageName);
                else
                    cmd = string.Format("-s {0} shell ps", deviceId);

                AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);
                var output = adb.Run(new[] { cmd }, "Unable to get the pid of the given packages.");
                if (string.IsNullOrEmpty(output))
                    return -1;

                if (device.SupportsFilteringByPid)
                {
                    AndroidLogcatInternalLog.Log(output);
                    return int.Parse(output);
                }

                return ParsePidInfo(packageName, output);
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log(ex.Message);
                return -1;
            }
        }

        public static string GetPackageNameFromPid(ADB adb, string deviceId, int processId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return string.Empty;

            try
            {
                string cmd = string.Format("-s {0} shell ps -p {1} -o NAME", deviceId, processId);

                AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);
                var output = adb.Run(new[] { cmd }, "Unable to get the package name for pid " + processId);
                if (string.IsNullOrEmpty(output))
                    return string.Empty;

                using (var sr = new StringReader(output))
                {
                    string line;
                    while ((line = sr.ReadLine().Trim()) != null)
                    {
                        if (line.Equals("NAME"))
                            continue;

                        return line;
                    }
                }

                AndroidLogcatInternalLog.Log("Unable to get the package name for pid " + processId + "\nOutput:\n" + output);
                return string.Empty;
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log(ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// Return the detail info of the given device.
        /// </summary>
        public static string RetrieveDeviceDetails(AndroidLogcatDevice device, string deviceId)
        {
            if (device == null)
                return deviceId;

            var manufacturer = device.Manufacturer;
            var model = device.Model;
            var release = device.OSVersion;
            var sdkVersion = device.APILevel;
            var abi = device.ABI;

            return string.Format("{0} {1} (version: {2}, abi: {3}, sdk: {4}, id: {5})", manufacturer, model, release, abi, sdkVersion, deviceId);
        }

        public static int ParsePidInfo(string packageName, string commandOutput)
        {
            string line = null;
            // Note: Regex is very slow, looping through string is much faster
            using (var sr = new StringReader(commandOutput))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.EndsWith(packageName))
                        break;
                }
            }

            if (string.IsNullOrEmpty(line))
            {
                AndroidLogcatInternalLog.Log("Cannot get process status for '{0}'.", packageName);
                return -1;
            }

            var regex = new Regex(@"\b\d+");
            Match match = regex.Match(line);
            if (!match.Success)
            {
                AndroidLogcatInternalLog.Log("Failed to parse pid of '{0}'from '{1}'.", packageName, line);
                return -1;
            }

            return int.Parse(match.Groups[0].Value);
        }

        public static int ParseTopActivityPackageInfo(string commandOutput, out string packageName)
        {
            packageName = "";
            if (string.IsNullOrEmpty(commandOutput))
                return -1;

            // Note: Regex is very slow, looping through string is much faster
            string line = null;
            using (var sr = new StringReader(commandOutput))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains("top-activity"))
                        break;
                }
            }

            if (string.IsNullOrEmpty(line))
            {
                AndroidLogcatInternalLog.Log("Cannot find top activity.");
                return -1;
            }
            AndroidLogcatInternalLog.Log(line);

            var reg = new Regex(@"(?<pid>\d{2,})\:(?<package>[^/]*)");
            var match = reg.Match(line);
            if (!match.Success)
            {
                AndroidLogcatInternalLog.Log("Match '{0}' failed.", line);
                return -1;
            }

            packageName = match.Groups["package"].Value;
            return int.Parse(match.Groups["pid"].Value);
        }

        public static void OpenTerminal(string workingDirectory)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe") {WorkingDirectory = workingDirectory});
                    break;
                case RuntimePlatform.OSXEditor:
                    var pathsToCheck = new[]
                    {
                        "/Applications/Utilities/Terminal.app/Contents/MacOS/Terminal",
                        "/System/Applications/Utilities/Terminal.app/Contents/MacOS/Terminal"
                    };
                    foreach (var p in pathsToCheck)
                    {
                        if (File.Exists(p))
                        {
                            System.Diagnostics.Process.Start(p, workingDirectory);
                            return;
                        }
                    }

                    throw new Exception(string.Format("Failed to launch Terminal app, tried following paths:\n{0}", string.Join("\n",  pathsToCheck)));
                default:
                    throw new Exception("Don't know how to open terminal on " + Application.platform.ToString());
            }
        }

        public static Version ParseVersionLegacy(string versionString)
        {
            int major = 0;
            int minor = 0;
            int build = 0;
            int revision = 0;
            var vals = versionString.Split('.');
            if (vals.Length > 0)
                int.TryParse(vals[0], out major);
            if (vals.Length > 1)
                int.TryParse(vals[1], out minor);
            if (vals.Length > 2)
                int.TryParse(vals[2], out build);
            if (vals.Length > 3)
                int.TryParse(vals[3], out revision);

            if (vals.Length <= 2)
                return new Version(major, minor);
            if (vals.Length <= 3)
                return new Version(major, minor, build);
            return new Version(major, minor, build, revision);
        }

        public static Version ParseVersion(string versionString)
        {
#if NET_2_0
            return ParseVersionLegacy(versionString);
#else
            var vals = versionString.Split('.');

            // Version.TryParse isn't capable of parsing digits without dots, for ex., 1
            if (vals.Length == 1)
            {
                int n;
                if (!int.TryParse(vals[0], out n))
                {
                    AndroidLogcatInternalLog.Log("Failed to parse android OS version '{0}'", versionString);
                    return new Version(0, 0);
                }
                return new Version(n, 0);
            }

            Version version;
            if (!Version.TryParse(versionString, out version))
            {
                AndroidLogcatInternalLog.Log("Failed to parse android OS version '{0}'", versionString);
                return new Version(0, 0);
            }
            return version;
#endif
        }

        public static BuildInfo ParseBuildInfo(string msg)
        {
            BuildInfo buildInfo;

            var reg = new Regex(@"Build type '(\S+)',\s+Scripting Backend '(\S+)',\s+CPU '(\S+)'");
            Match match = reg.Match(msg);

            buildInfo.buildType = match.Groups[1].Value.ToLower();
            buildInfo.scriptingImplementation = match.Groups[2].Value.ToLower();
            buildInfo.cpu = match.Groups[3].Value.ToLower();
            return buildInfo;
        }

        /// <summary>
        /// Returns symbol file by checking following extensions, for ex., if you're searching for libunity.so symbol file, it will first try to:
        /// - libunity.so
        /// - libunity.sym.so
        /// - libunity.dbg.so
        /// </summary>
        /// <param name="symbolPath"></param>
        /// <param name="libraryFile"></param>
        /// <returns></returns>
        public static string GetSymbolFile(string symbolPath, string libraryFile)
        {
            var fullPath = Path.Combine(symbolPath, libraryFile);
            if (File.Exists(fullPath))
                return fullPath;

            var extensionsToTry = new[] { ".sym.so", ".dbg.so" };
            foreach (var e in extensionsToTry)
            {
                // Try sym.so extension
                fullPath = Path.Combine(symbolPath, Path.GetFileNameWithoutExtension(libraryFile) + e);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }
    }

    internal class AndroidLogcatJsonSerialization
    {
        public string m_SelectedDeviceId = String.Empty;

        public AndroidLogcatConsoleWindow.PackageInformation m_SelectedPackage = null;

        public AndroidLogcat.Priority m_SelectedPriority = AndroidLogcat.Priority.Verbose;

        public List<AndroidLogcatConsoleWindow.PackageInformation> m_PackagesForSerialization = null;

        public AndroidLogcatTagsControl m_TagControl = null;
    }
}
#else
namespace Unity.Android.Logcat
{
    internal class AndroidLogcatUtilities
    {
        public static void ShowActivePlatformNotAndroidMessage()
        {
            UnityEditor.EditorGUILayout.HelpBox("Please switch active platform to be Android in Build Settings Window.", UnityEditor.MessageType.Info);
        }
    }
}
#endif
