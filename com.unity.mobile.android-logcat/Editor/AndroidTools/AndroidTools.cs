#if PLATFORM_ANDROID
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Unity.Android.Logcat
{
    internal class AndroidTools
    {
        private string m_NDKDirectory;
        private string m_Addr2LinePath;
        private string m_NMPath;
        private string m_ReadElfPath;

        internal AndroidTools()
        {
            string platformTag = "windows-x86_64";
            if (Application.platform != RuntimePlatform.WindowsEditor)
                platformTag = "darwin-x86_64";
#if UNITY_2019_3_OR_NEWER
            m_NDKDirectory = UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath;
            var binPath = Paths.Combine(m_NDKDirectory, "toolchains", "llvm", "prebuilt", platformTag, "bin");
            m_NMPath = Path.Combine(binPath, "llvm-nm");
#else
            m_NDKDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(UnityEditor.Android.ADB.GetInstance().GetADBPath()), @"..\..\NDK"));
            if (!Directory.Exists(m_NDKDirectory))
            {
                m_NDKDirectory = EditorPrefs.GetString("AndroidNdkRootR16b");
                if (!Directory.Exists(m_NDKDirectory))
                {
                    m_NDKDirectory = string.Empty;
                    throw new System.Exception("Failed to locate NDK directory");
                }
            }

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

            // Addr2Line is important for us, so show an error, if it's not found
            if (!File.Exists(m_Addr2LinePath))
                Debug.LogError("Failed to locate " + m_Addr2LinePath);
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
            var result = Shell.RunProcess(
                m_Addr2LinePath,
                "-C -f -p -e \"" + symbolFilePath + "\" " + string.Join(" ", addresses.ToArray()));
            ValidateResult(result);
            return result.GetStandardOut().Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        internal string[] RunNM(string symbolFilePath)
        {
            var result = Shell.RunProcess(
                m_NMPath,
                "-extern-only \"" + symbolFilePath + "\"",
                Path.GetDirectoryName(m_NMPath));
            ValidateResult(result);
            return result.GetStandardOut().Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        internal string[] RunReadElf(string symbolFilePath)
        {
            var result = Shell.RunProcess(
                m_ReadElfPath,
                "-Ws \"" + symbolFilePath + "\"",
                Path.GetDirectoryName(m_NMPath));
            ValidateResult(result);
            return result.GetStandardOut().Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
#endif
