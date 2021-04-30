using System;
using System.IO;
using System.Linq;
using NUnit.Framework.Interfaces;
using UnityEngine;

namespace Unity.Android.Logcat
{
    internal class AndroidTools
    {
        private string m_NDKDirectory;
        private string m_Addr2LinePath;
        private string m_NMPath;
        private string m_ReadElfPath;
        private AndroidBridge.ADB m_ADB;

        internal AndroidTools()
        {
        }

        private void ResolvePathsIfNeeded()
        {
            string platformTag = "windows-x86_64";
            if (Application.platform != RuntimePlatform.WindowsEditor)
                platformTag = "darwin-x86_64";
#if UNITY_2019_3_OR_NEWER
            var ndkDirectory = AndroidBridge.AndroidExternalToolsSettings.ndkRootPath;
#else
            var directoriesToChecks = new[]
            {
                Path.GetFullPath(Path.Combine(Path.GetDirectoryName(AndroidBridge.ADB.GetInstance().GetADBPath()), @"..\..\NDK")),
                UnityEditor.EditorPrefs.GetString("AndroidNdkRootR16b"),
                System.Environment.GetEnvironmentVariable("ANDROID_NDK_ROOT")
            };

            var ndkDirectory = string.Empty;
            foreach (var d in directoriesToChecks)
            {
                if (string.IsNullOrEmpty(d))
                    continue;
                if (!Directory.Exists(d))
                    continue;
                ndkDirectory = d;
                break;
            }
#endif
            if (string.IsNullOrEmpty(ndkDirectory))
                throw new Exception("Failed to locate NDK directory");

            var sourceProperties = Path.Combine(ndkDirectory, "source.properties");
            if (!File.Exists(sourceProperties))
                throw new Exception($"NDK directory '{ndkDirectory}' doesn't exist or is invalid (Failed to locate source.properties), please set it in Preferences->External Tools.");

            if (!string.IsNullOrEmpty(ndkDirectory) && !string.IsNullOrEmpty(m_NDKDirectory) && m_NDKDirectory.Equals(ndkDirectory))
                return;

            m_NDKDirectory = ndkDirectory;

#if UNITY_2019_3_OR_NEWER
            var binPath = Paths.Combine(m_NDKDirectory, "toolchains", "llvm", "prebuilt", platformTag, "bin");
            m_NMPath = Path.Combine(binPath, "llvm-nm");
#else
            var binPath = Paths.Combine(m_NDKDirectory, "toolchains", "aarch64-linux-android-4.9", "prebuilt", platformTag, "bin");
            m_NMPath = Path.Combine(binPath, "aarch64-linux-android-nm");
#endif

            m_Addr2LinePath = Path.Combine(binPath, "aarch64-linux-android-addr2line");
            m_ReadElfPath = Path.Combine(binPath, "aarch64-linux-android-readelf");
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                m_Addr2LinePath += ".exe";
                m_NMPath += ".exe";
                m_ReadElfPath += ".exe";
            }
        }

        internal void ValidateResult(ShellReturnInfo result)
        {
            if (result.GetExitCode() == 0)
                return;
            throw new System.Exception(string.Format("{0} {1}\nreturned with exit code {2}\nWorking Directory:\n{3}\nStandardOutput:\n{4}\nStandardError:\n{5}",
                result.GetStartInfo().FileName, result.GetStartInfo().Arguments,
                result.GetExitCode(),
                result.GetStartInfo().WorkingDirectory,
                result.GetStandardErr(),
                result.GetStandardOut()));
        }

        internal string[] RunAddr2Line(string symbolFilePath, string[] addresses)
        {
            ResolvePathsIfNeeded();

            // Addr2Line is important for us, so show an error, if it's not found
            if (!File.Exists(m_Addr2LinePath))
                throw new Exception("Failed to locate " + m_Addr2LinePath);

            // https://sourceware.org/binutils/docs/binutils/addr2line.html
            var args = "-C -f -p -e \"" + symbolFilePath + "\" " + string.Join(" ", addresses.ToArray());
            AndroidLogcatInternalLog.Log($"\"{m_Addr2LinePath}\" {args}");
            var result = Shell.RunProcess(
                m_Addr2LinePath, args);
            ValidateResult(result);
            return result.GetStandardOut().Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        internal string[] RunNM(string symbolFilePath)
        {
            ResolvePathsIfNeeded();

            if (!File.Exists(m_NMPath))
                throw new Exception("Failed to locate " + m_NMPath);

            var result = Shell.RunProcess(
                m_NMPath,
                "-extern-only \"" + symbolFilePath + "\"",
                Path.GetDirectoryName(m_NMPath));
            ValidateResult(result);
            return result.GetStandardOut().Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        internal string[] RunReadElf(string symbolFilePath)
        {
            ResolvePathsIfNeeded();

            if (!File.Exists(m_ReadElfPath))
                throw new Exception("Failed to locate " + m_ReadElfPath);

            var result = Shell.RunProcess(
                m_ReadElfPath,
                "-Ws \"" + symbolFilePath + "\"",
                Path.GetDirectoryName(m_NMPath));
            ValidateResult(result);
            return result.GetStandardOut().Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        internal AndroidBridge.ADB ADB
        {
            get
            {
                if (m_ADB == null)
                    m_ADB = AndroidBridge.ADB.GetInstance();

                return m_ADB;
            }
        }
    }
}
