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

        internal AndroidTools()
        {
#if UNITY_2019_3_OR_NEWER
            m_NDKDirectory = AndroidExternalToolsSettings.ndkRootPath;
#else
            m_NDKDirectory = EditorPrefs.GetString("AndroidNdkRootR16b");
#endif

            var binPath = Paths.Combine(m_NDKDirectory, "toolchains", "llvm", "prebuilt", "windows-x86_64", "bin");
            m_Addr2LinePath = Path.Combine(binPath, "aarch64-linux-android-addr2line");
            m_NMPath = @"H:\Android\android-ndk-r16b\toolchains\x86_64-4.9\prebuilt\windows-x86_64\x86_64-linux-android\bin\nm";// Path.Combine(binPath, "llvm -nm");
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                m_Addr2LinePath += ".exe";
                m_NMPath += ".exe";
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
    }
}
