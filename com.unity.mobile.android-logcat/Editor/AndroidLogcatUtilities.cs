using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    internal static class AndroidLogcatUtilities
    {
        internal static readonly string kAbiArm64 = "arm64-v8a";
        internal static readonly string kAbiArmV7 = "armeabi-v7a";
        internal static readonly string kAbiX86 = "x86";
        internal static readonly string kAbiX86_64 = "x86-64";

        /// <summary>
        /// Capture the screenshot on the given device.
        /// </summary>
        /// <returns> Return the path to the screenshot on the PC. </returns>
        public static bool CaptureScreen(AndroidBridge.ADB adb, string deviceId, string imagePathOnHost, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(deviceId))
            {
                error = "Invalid device id.";
                return false;
            }

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
                    error = outputMsg;
                    return false;
                }

                cmd = string.Format("-s {0} pull \"{1}\" \"{2}\"", deviceId, screenshotPathOnDevice, imagePathOnHost);
                AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);

                errorMsg = "Unable to pull the screenshot from device ";
                outputMsg = adb.Run(new[] { cmd }, errorMsg + deviceId);
                if (outputMsg.StartsWith(errorMsg))
                {
                    AndroidLogcatInternalLog.Log(outputMsg);
                    Debug.LogError(outputMsg);
                    error = outputMsg;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log("Exception caugth while capturing screen on device {0}. Details\r\n:{1}", deviceId, ex);
                error = ex.Message;
                return false;
            }
        }

        public static string GetTemporaryPath(IAndroidLogcatDevice device, string name, string extension)
        {
            string fileName = device != null ? SanitizeFileName(device.Id) : "NoDevice";
            fileName = $"{name}_{fileName}{extension}";
            return Path.Combine(Application.dataPath, "..", "Temp", fileName).Replace("\\", "/");
        }

        /// <summary>
        /// Replaces anything the filesystem will not accept in a file name. A device id
        /// can be an ip:port, and ':' is not allowed on Windows.
        /// </summary>
        public static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        /// <summary>
        /// What the OS calls its file browser, for menu items that reveal a file in it.
        /// </summary>
        public static string RevealInFileBrowserLabel
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.OSXEditor: return "Show In Finder";
                    case RuntimePlatform.LinuxEditor: return "Show In File Manager";
                    default: return "Show In Explorer";
                }
            }
        }

        /// <summary>Selects a file in the OS file browser, rather than opening it.</summary>
        public static void RevealInFileBrowser(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            UnityEditor.EditorUtility.RevealInFinder(path);
        }

        /// <summary>Opens a file with whatever the OS uses for its type.</summary>
        public static void OpenFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                    // Application.OpenURL on a plain path does nothing useful on macOS.
                    System.Diagnostics.Process.Start("open", path);
                    break;
                default:
                    Application.OpenURL(path);
                    break;
            }
        }

        /// <summary>
        /// Asks where to put a copy of <paramref name="sourcePath"/> and copies it there.
        /// The extension offered in the dialog comes from the source file, so callers do
        /// not have to know it.
        /// </summary>
        /// <returns>
        /// The directory saved into, so the caller can remember it, or null if the dialog
        /// was cancelled or the copy failed. A failure is logged.
        /// </returns>
        public static string SaveFileAs(string sourcePath, string title, string startDirectory)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                return null;

            var extension = Path.GetExtension(sourcePath);
            var path = UnityEditor.EditorUtility.SaveFilePanel(title, startDirectory,
                Path.GetFileName(sourcePath),
                string.IsNullOrEmpty(extension) ? string.Empty : extension.Substring(1));
            if (string.IsNullOrEmpty(path))
                return null;

            try
            {
                File.Copy(sourcePath, path, true);
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("Failed to save '{0}' as '{1}'.\n{2}", sourcePath, path, ex.Message);
                return null;
            }

            // A screenshot's details file goes with the copy. Nothing to do for a
            // file that has none, which is every video.
            AndroidLogcatScreenshotInfo.CopyBeside(sourcePath, path);

            return Path.GetFullPath(Path.GetDirectoryName(path));
        }

        /// <summary>
        /// Where captured screenshots are kept: under Library, which is per machine and
        /// gitignored, and outside Assets so Unity never imports the images as assets.
        /// Not UserSettings - that is for settings, and these are output.
        /// <para>
        /// Library is also deleted from time to time, by hand or by the Editor. That is
        /// the right trade for debugging output: a screenshot worth keeping is one Save
        /// As away from somewhere that is not Library.
        /// </para>
        /// </summary>
        public static string GetScreenshotsDirectory()
        {
            return GetCaptureDirectory("Screenshots");
        }

        /// <summary>
        /// Where the Layout Viewer keeps its screenshot. Its own directory, not the one
        /// above: that capture belongs to the layout it was queried with and is replaced
        /// by the next query, so it has no business in the saved screenshot list.
        /// </summary>
        public static string GetLayoutViewDirectory()
        {
            return GetCaptureDirectory("LayoutView");
        }

        static string GetCaptureDirectory(string name)
        {
            var path = Path.Combine(Application.dataPath, "..", "Library", "AndroidLogcat", name);
            return Path.GetFullPath(path).Replace("\\", "/");
        }


        internal static string ResolvePath(params string[] relativeParts)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(AndroidLogcatUtilities).Assembly);
            if (package == null)
                return null;

            var parts = new string[relativeParts.Length + 1];
            parts[0] = package.resolvedPath;
            Array.Copy(relativeParts, 0, parts, 1, relativeParts.Length);
            return Path.GetFullPath(Path.Combine(parts));
        }

        /// <summary>
        /// The path with the project folder stripped off, for showing in the UI. A
        /// screenshot's absolute path is mostly project folder, which in a tooltip is
        /// wide enough to cover the rows around it.
        /// </summary>
        public static string ProjectRelativePath(string path)
        {
            return ProjectRelativePath(path, GetProjectDirectory());
        }

        /// <summary>
        /// The same, against a given project folder rather than this project's, so that
        /// it can be exercised with paths from a platform other than the one running.
        /// </summary>
        internal static string ProjectRelativePath(string path, string projectDirectory)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // Trailing slash trimmed so that the separator check below has a separator
            // to find, whatever shape the folder was handed over in.
            var project = projectDirectory.Replace("\\", "/").TrimEnd('/');
            var normalized = path.Replace("\\", "/");

            if (normalized.Length > project.Length + 1
                && normalized[project.Length] == '/'
                && normalized.StartsWith(project, StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(project.Length + 1);

            // Not under the project - a screenshot opened from elsewhere, say - so there
            // is nothing to strip and the whole path is the most useful thing to show.
            return normalized;
        }

        static string s_ProjectDirectory;

        /// <summary>
        /// The folder that holds Assets, cached: tooltips are built per row per repaint,
        /// and the project does not move while the Editor is running.
        /// </summary>
        static string GetProjectDirectory()
        {
            if (s_ProjectDirectory == null)
                s_ProjectDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace("\\", "/");
            return s_ProjectDirectory;
        }

        // Long enough for a first run, which downloads Gradle itself.
        const int kGradleTimeoutMs = 5 * 60 * 1000;
        const int kGradleProgressUpdateMs = 200;

        /// <summary>
        /// Runs a Gradle task in a project directory and says whether it succeeded,
        /// logging its output either way.
        /// <para>
        /// The JDK and SDK come from Unity's own External Tools settings rather than
        /// from the environment: the Editor may not have inherited a shell environment
        /// at all, the one it did inherit is not necessarily the one this build wants,
        /// and a user who pointed Unity at their own SDK or JDK means it. The wrapper
        /// is run through <c>sh</c> off Windows, so that this does not depend on its
        /// executable bit, which is invisible to anyone working from Windows.
        /// </para>
        /// <para>
        /// Blocking, behind a progress bar. This is a developer action - there is no
        /// hot path here - and a Gradle build wants the Editor to sit still anyway.
        /// </para>
        /// </summary>
        /// <summary>
        /// Kills a process and whatever it started. Gradle runs behind a launcher
        /// script and does its work in a daemon, so killing only the process we started
        /// leaves the build running.
        /// </summary>
        static void KillProcessTree(System.Diagnostics.Process process)
        {
            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    var killer = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/T /F /PID {process.Id}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    killer?.WaitForExit(5000);
                    killer?.Dispose();
                }
                else
                {
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to stop Gradle.\n{ex.Message}");
            }
        }

        internal static bool RunGradle(string projectDirectory, string task)
        {
            if (string.IsNullOrEmpty(projectDirectory) || !Directory.Exists(projectDirectory))
            {
                Debug.LogError($"No Gradle project at '{projectDirectory}'.");
                return false;
            }

            string androidHome;
            string javaHome;
            try
            {
                androidHome = AndroidBridge.AndroidExternalToolsSettings.sdkRootPath;
                javaHome = AndroidBridge.AndroidExternalToolsSettings.jdkRootPath;
            }
            catch (Exception ex)
            {
                Debug.LogError("Could not read the Android SDK and JDK locations from " +
                    $"Preferences > External Tools.\n{ex.Message}");
                return false;
            }

            var windows = Application.platform == RuntimePlatform.WindowsEditor;

            var process = new System.Diagnostics.Process();
            var si = process.StartInfo;
            si.WorkingDirectory = projectDirectory;
            si.FileName = windows ? Path.Combine(projectDirectory, "gradlew.bat") : "sh";
            si.Arguments = windows ? task : $"gradlew {task}";
            // Left unset when a path is not configured, rather than pointed at nothing:
            // Gradle then falls back to local.properties or an inherited variable, which
            // is a better answer than a directory that does not exist.
            if (!string.IsNullOrEmpty(javaHome) && Directory.Exists(javaHome))
                si.EnvironmentVariables["JAVA_HOME"] = javaHome;
            if (!string.IsNullOrEmpty(androidHome) && Directory.Exists(androidHome))
                si.EnvironmentVariables["ANDROID_HOME"] = androidHome;
            si.UseShellExecute = false;
            si.CreateNoWindow = true;
            si.RedirectStandardOutput = true;
            si.RedirectStandardError = true;

            var output = new System.Text.StringBuilder();
            // What Gradle said last, which is the only sign of progress it gives while
            // a build runs.
            var lastLine = string.Empty;

            try
            {
                var title = $"Running Gradle in {Path.GetFileName(projectDirectory)}";
                var command = $"{si.FileName} {si.Arguments}";
                EditorUtility.DisplayProgressBar(title, command, 0);

                System.Diagnostics.DataReceivedEventHandler record = (s, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data))
                        return;
                    // Straight to Editor.log as it arrives: the collected log is only
                    // reported once the build is over, which is no help while watching
                    // one that is stuck.
                    Console.WriteLine(e.Data);
                    lock (output)
                    {
                        output.AppendLine(e.Data);
                        lastLine = e.Data;
                    }
                };

                process.OutputDataReceived += record;
                process.ErrorDataReceived += record;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var started = DateTime.Now;
                while (!process.WaitForExit(kGradleProgressUpdateMs))
                {
                    string message;
                    lock (output)
                        message = string.IsNullOrEmpty(lastLine) ? command : lastLine;

                    var elapsed = DateTime.Now - started;
                    // Gradle reports no progress of its own, so the bar only says the
                    // build is still alive - it fills over ten seconds and starts over.
                    var progress = (float)(elapsed.TotalSeconds % 10.0) / 10.0f;

                    if (EditorUtility.DisplayCancelableProgressBar(title, message, progress))
                    {
                        KillProcessTree(process);
                        Debug.LogWarning($"'gradlew {task}' was cancelled.");
                        return false;
                    }

                    if (elapsed.TotalMilliseconds >= kGradleTimeoutMs)
                    {
                        KillProcessTree(process);
                        Debug.LogError($"Gradle did not finish within {kGradleTimeoutMs / 1000} s.");
                        return false;
                    }
                }

                // The redirected output is read on other threads, and the wait above
                // only waits for the process: this one waits for that output too.
                process.WaitForExit();

                string log;
                lock (output)
                    log = output.ToString();
                AndroidLogcatInternalLog.Log(log);

                if (process.ExitCode != 0)
                {
                    Debug.LogError($"'gradlew {task}' failed with exit code {process.ExitCode}.\n{log}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to run Gradle in '{projectDirectory}'.\n{ex.Message}");
                return false;
            }
            finally
            {
                process.Dispose();
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Get the top activity on the given device.
        /// </summary>
        public static bool GetTopActivityInfo(AndroidBridge.ADB adb, IAndroidLogcatDevice device, ref string packageName, ref int packagePid)
        {
            if (device == null)
                return false;
            try
            {
                var cmd = "-s " + device.Id + " shell \"dumpsys activity\" ";
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
        public static int GetPidFromPackageName(AndroidBridge.ADB adb, IAndroidLogcatDevice device, string packageName)
        {
            if (device == null)
                return -1;

            try
            {
                string cmd = null;
                if (device.SupportsFilteringByPid)
                    cmd = string.Format("-s {0} shell pidof -s {1}", device.Id, packageName);
                else
                    cmd = string.Format("-s {0} shell ps", device.Id);

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
                AndroidLogcatInternalLog.Log($"Failed to get process id for {packageName}:\n{ex.Message}");
                return -1;
            }
        }

        public static bool KillProcesss(AndroidBridge.ADB adb, IAndroidLogcatDevice device, int pid)
        {
            try
            {
                var cmd = $"-s {device.Id} shell kill {pid}";
                AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);
                adb.Run(new[] { cmd }, $"Unable to kill process {pid}");
                return true;
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log($"Failed to kill process with process id {pid}:\n{ex.Message}");
                return false;
            }
        }

        internal static string ProcessOutputFromPS(string psOutput)
        {
            using (var sr = new StringReader(psOutput))
            {
                string line;
                while ((line = sr.ReadLine().Trim()) != null)
                {
                    if (line.Contains("NAME"))
                        continue;

                    // The process name is always the last split
                    var entries = line.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    if (entries.Length > 0)
                        return entries[entries.Length - 1];
                }
            }

            return string.Empty;
        }

        public static string GetProcessNameFromPid(AndroidBridge.ADB adb, IAndroidLogcatDevice device, int processId)
        {
            if (device == null)
                return string.Empty;

            return GetProcessNameFromPid(adb, device.Id, processId);
        }

        public static string GetProcessNameFromPid(AndroidBridge.ADB adb, string deviceId, int processId)
        {

            try
            {
                // Note: Flag -o doesn't work on Android 5.0 devices (tested on LGE LG-D620, 5.0.2)
                string cmd = string.Format("-s {0} shell ps -p {1}", deviceId, processId);

                AndroidLogcatInternalLog.Log("{0} {1}", adb.GetADBPath(), cmd);
                var output = adb.Run(new[] { cmd }, "Unable to get the process name for pid " + processId);
                if (string.IsNullOrEmpty(output))
                    return string.Empty;

                var result = ProcessOutputFromPS(output);
                if (string.IsNullOrEmpty(result))
                    AndroidLogcatInternalLog.Log("Unable to get the process name for pid " + processId + "\nOutput:\n" + output);
                return result;
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
                do
                {
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Contains("top-activity") ||        // Top Activity when device is not locked
                            line.Contains("top-sleeping"))          // Top Activity when device is locked
                            break;
                    }

                    if (string.IsNullOrEmpty(line))
                    {
                        AndroidLogcatInternalLog.Log("Cannot find top activity.");
                        return -1;
                    }

                    AndroidLogcatInternalLog.Log(line);

                    var reg = new Regex(@"(?<pid>\d+)\:(?<package>\S+)\/\S+\s+\(top-\S+\)");
                    var match = reg.Match(line);
                    if (!match.Success)
                    {
                        AndroidLogcatInternalLog.Log("Match '{0}' failed.", line);
                        return -1;
                    }

                    int pid = int.Parse(match.Groups["pid"].Value);

                    // There can be lines with (top-activity) at the end, but pid == 0, not sure what are those, but definetly not top activities
                    if (pid > 0)
                    {
                        packageName = match.Groups["package"].Value;
                        return pid;
                    }

                    // Continue looking for top activity
                }
                while (true);
            }
        }

        public static void OpenTerminal(string workingDirectory)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe") { WorkingDirectory = workingDirectory });
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

                    throw new Exception(string.Format("Failed to launch Terminal app, tried following paths:\n{0}", string.Join("\n", pathsToCheck)));
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
        internal static string GetSymbolFile(string symbolPath, string libraryFile, string[] extensionsToTry)
        {
            var fullPath = Path.GetFullPath(Path.Combine(symbolPath, libraryFile));
            if (File.Exists(fullPath))
                return fullPath;

            foreach (var e in extensionsToTry)
            {
                // Try sym.so extension
                fullPath = Path.GetFullPath(Path.Combine(symbolPath, Path.GetFileNameWithoutExtension(libraryFile) + e));
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return string.Empty;
        }

        internal static string GetSymbolFile(IReadOnlyList<ReordableListItem> symbolPaths, string abi, string libraryFile, string[] extensionsToTry)
        {
            foreach (var symbolPath in symbolPaths)
            {
                if (!symbolPath.Enabled)
                    continue;

                if (!string.IsNullOrEmpty(abi))
                {
                    var fileWithABI = GetSymbolFile(Path.Combine(symbolPath.Name, abi), libraryFile, extensionsToTry);
                    if (!string.IsNullOrEmpty(fileWithABI))
                        return fileWithABI;
                }

                var file = GetSymbolFile(symbolPath.Name, libraryFile, extensionsToTry);
                if (!string.IsNullOrEmpty(file))
                    return file;
            }

            return string.Empty;
        }

        internal static bool ParseCrashLine(IReadOnlyList<ReordableListItem> regexs, string msg, out string abi, out string address, out string libName, out string buildId)
        {
            abi = string.Empty;
            foreach (var regexItem in regexs)
            {
                if (!regexItem.Enabled)
                    continue;

                var match = new Regex(regexItem.Name).Match(msg);
                if (match.Success)
                {
                    var rawAbi = match.Groups["abi"].Value;
                    if (!string.IsNullOrEmpty(rawAbi))
                    {
                        if (rawAbi.Equals("arm"))
                            abi = kAbiArmV7;
                        else if (rawAbi.Equals("arm64"))
                            abi = kAbiArm64;
                        else if (rawAbi.Equals("x86"))
                            abi = kAbiX86;
                        else if (rawAbi.Equals("x86_64"))
                            abi = kAbiX86_64;
                    }

                    address = match.Groups["address"].Value;
                    libName = match.Groups["libName"].Value + ".so";
                    buildId = match.Groups["buildId"].Value;
                    return true;
                }
            }

            address = null;
            libName = null;
            buildId = null;
            return false;
        }

        internal static void KillScreenRecordProcess(AndroidLogcatRuntimeBase runtime, IAndroidLogcatDevice device)
        {
            if (device == null)
                return;
            var pid = GetPidFromPackageName(runtime.Tools.ADB, device, "screenrecord");
            if (pid != -1)
                KillProcesss(runtime.Tools.ADB, device, pid);
        }

        internal static void ShowAndroidIsNotInstalledMessage()
        {
            UnityEditor.EditorGUILayout.HelpBox("Android Logcat requires Android support to be installed.", UnityEditor.MessageType.Info);
        }

        internal static void ApplySettings(AndroidLogcatRuntimeBase runtime, AndroidLogcat logcat)
        {
            if (runtime == null)
                throw new ArgumentNullException("AndroidLogcatRuntimeBase is null");
            var settings = runtime.Settings;
            var userSettings = runtime.UserSettings;
            var selectedDevice = runtime.DeviceQuery.SelectedDevice;

            int fixedHeight = settings.MessageFontSize + 5;
            AndroidLogcatStyles.kLogEntryFontSize = settings.MessageFontSize;
            AndroidLogcatStyles.kLogEntryFixedHeight = fixedHeight;
            AndroidLogcatStyles.background.fixedHeight = fixedHeight;
            AndroidLogcatStyles.backgroundEven.fixedHeight = fixedHeight;
            AndroidLogcatStyles.backgroundOdd.fixedHeight = fixedHeight;
            AndroidLogcatStyles.priorityDefaultStyle.font = settings.MessageFont;
            AndroidLogcatStyles.priorityDefaultStyle.fontSize = settings.MessageFontSize;
            AndroidLogcatStyles.priorityDefaultStyle.fixedHeight = fixedHeight;
            foreach (var p in (Priority[])Enum.GetValues(typeof(Priority)))
            {
                AndroidLogcatStyles.priorityStyles[(int)p].normal.textColor = settings.GetMessageColor(p);
                AndroidLogcatStyles.priorityStyles[(int)p].font = settings.MessageFont;
                AndroidLogcatStyles.priorityStyles[(int)p].fontSize = settings.MessageFontSize;
                AndroidLogcatStyles.priorityStyles[(int)p].fixedHeight = fixedHeight;
            }

            logcat?.StripFilteredEntriesIfNeeded();
            logcat?.StripRawEntriesIfNeeded();
            userSettings.CleanupDeadProcessesForDevice(selectedDevice, settings.MaxExitedPackagesToShow);
        }

        // When we use / in context menu, this creates submenu, which is no good
        // Replace it with unicode slash, while it won't display this in pretty way, it's still better than not displaying anything
        internal static string FixSlashesForIMGUI(string value)
        {
            return value.Replace("/", " \u2215");
        }

        internal static bool FileExists(AndroidLogcatRuntimeBase runtime, IAndroidLogcatDevice device, string path)
        {
            try
            {
                var result = runtime.Tools.ADB.Run(new[] { $"-s {device.Id}", "shell", "ls", path }, $"Couldn't query '{path}'");
                return path.Equals(result);
            }
            catch
            {
                return false;
            }
        }

        internal static string[] GetEnabledValues(this IReadOnlyList<ReordableListItem> list)
        {
            return list.Where(i => i.Enabled).Select(i => i.Name).ToArray();
        }

        internal static bool HasCtrlOrCmdModifier(this Event e)
        {
            return (e.modifiers & (Application.platform == RuntimePlatform.OSXEditor ? EventModifiers.Command : EventModifiers.Control)) != 0;
        }
        internal static string GetPlaybackEngineDirectory()
        {
            return BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None);
        }

        internal static string GetBuildId(AndroidTools tools, string path)
        {
            try
            {
                var contents = tools.RunReadElf($"-W -x .note.gnu.build-id \"{path}\"");
                var combined = string.Join("\n", contents);

                if (contents.Length != 4)
                {
                    AndroidLogcatInternalLog.Log($"Failed to get build id for '{path}', expected 4 lines, but got\n{combined}");
                    return string.Empty;
                }

                var tag = "Hex dump of section '.note.gnu.build-id':";
                if (!contents[0].Contains(tag))
                {
                    AndroidLogcatInternalLog.Log($"Failed to get build id for '{path}', expected '{tag}' line, but got\n{combined}");
                    return string.Empty;
                }

                // Parsing content like
                //Hex dump of section '.note.gnu.build-id':
                //  0x00000200 04000000 14000000 03000000 474e5500............GNU.
                //  0x00000210 4d911593 b4008c72 50197a60 a320d872 M......rP.z`. .r
                //  0x00000220 575ecc1b W^..

                var splits = contents[2].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var buildId = splits[1] + splits[2] + splits[3] + splits[4];
                splits = contents[3].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                buildId += splits[1];

                return buildId.ToLowerInvariant();
            }
            catch (Exception e)
            {
                AndroidLogcatInternalLog.Log($"Failed to get build id for '{path}'\n{e.ToString()}");
                return string.Empty;
            }
        }

        internal static VisualTreeAsset LoadUXML(string uxmlFileName)
        {
            var path = $"Packages/com.unity.mobile.android-logcat/Editor/UI/Layouts/{uxmlFileName}";
            var result = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"Packages/com.unity.mobile.android-logcat/Editor/UI/Layouts/{uxmlFileName}");
            if (result == null)
                throw new FileNotFoundException($"Failed to load '{path}'");
            return result;
        }

        internal static void DrawProgressIcon(bool inProgress)
        {
            var statusIcon = GUIContent.none;
            if (inProgress)
            {
                int frame = (int)Mathf.Repeat(Time.realtimeSinceStartup * 10, 11.99f);
                statusIcon = AndroidLogcatStyles.Status.GetContent(frame);
            }
            GUILayout.Label(statusIcon, AndroidLogcatStyles.StatusIcon, GUILayout.Width(30));
        }
        internal static void DrawRectangle(Rect area, int frameWidth, Color color)
        {
            var texture = EditorGUIUtility.whiteTexture;
            var oldColor = GUI.color;
            GUI.color = color;

            var lineArea = area;
            lineArea.height = frameWidth;
            GUI.DrawTexture(lineArea, texture);
            lineArea.y = area.yMax - frameWidth;
            GUI.DrawTexture(lineArea, texture);
            lineArea = area;
            lineArea.width = frameWidth;
            GUI.DrawTexture(lineArea, texture);
            lineArea.x = area.xMax - frameWidth;

            GUI.DrawTexture(lineArea, texture);
            GUI.color = oldColor;
        }
    }
}
