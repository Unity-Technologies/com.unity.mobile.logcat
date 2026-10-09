using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Unity.Android.Logcat
{
    internal class ShellStartInfo
    {
        internal string FileName { set; get; }
        internal string Arguments { set; get; }
        internal string WorkingDirectory { set; get; }

        internal ShellStartInfo()
        {
            FileName = string.Empty;
            Arguments = string.Empty;
            WorkingDirectory = string.Empty;
        }
    }

    internal class ShellReturnInfo
    {
        private readonly ShellStartInfo m_StartInfo;
        private readonly int m_ExitCode;
        private readonly string m_StandardOut;
        private readonly string m_StandardErr;

        internal ShellStartInfo GetStartInfo()
        {
            return m_StartInfo;
        }

        internal int GetExitCode()
        {
            return m_ExitCode;
        }

        internal string GetStandardOut()
        {
            return m_StandardOut;
        }

        internal string GetStandardErr()
        {
            return m_StandardErr;
        }

        internal ShellReturnInfo(ShellStartInfo startInfo, int exitCode, string standardOut, string standardErr)
        {
            m_StartInfo = startInfo;
            m_ExitCode = exitCode;
            m_StandardOut = standardOut;
            m_StandardErr = standardErr;
        }
    }

    internal static class Shell
    {
        internal static ShellReturnInfo RunProcess(string fileName, string arguments)
        {
            return RunProcess(new ShellStartInfo() { FileName = fileName, Arguments = arguments });
        }

        internal static ShellReturnInfo RunProcess(string fileName, string arguments, string workingDirectory)
        {
            return RunProcess(new ShellStartInfo() { FileName = fileName, Arguments = arguments, WorkingDirectory = workingDirectory });
        }

        /// <summary>
        /// Runs a process, killing it if it doesn't finish within <paramref name="timeoutMs"/>.
        /// Use this for commands supplied by the user, which may never exit on their own.
        /// </summary>
        internal static ShellReturnInfo RunProcess(string fileName, string arguments, int timeoutMs, out bool timedOut)
        {
            return RunProcess(new ShellStartInfo() { FileName = fileName, Arguments = arguments }, timeoutMs, out timedOut);
        }

        internal static ShellReturnInfo RunProcess(ShellStartInfo startInfo)
        {
            return RunProcess(startInfo, Timeout.Infinite, out _);
        }

        internal static ShellReturnInfo RunProcess(ShellStartInfo startInfo, int timeoutMs, out bool timedOut)
        {
            Process process = new Process();
            process.StartInfo.FileName = startInfo.FileName;
            process.StartInfo.Arguments = startInfo.Arguments;
            process.StartInfo.WorkingDirectory = startInfo.WorkingDirectory;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            // The data received handlers are invoked on threadpool threads, so all access to these
            // builders is guarded by a lock - StringBuilder is not thread safe.
            var streamLock = new object();
            var output = new StringBuilder();
            process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (streamLock)
                        output.AppendLine(e.Data);
                }
            });

            var error = new StringBuilder();
            process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (streamLock)
                        error.AppendLine(e.Data);
                }
            });

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            timedOut = false;
            if (timeoutMs == Timeout.Infinite)
            {
                process.WaitForExit();
            }
            else if (process.WaitForExit(timeoutMs))
            {
                // WaitForExit(int) only waits for the process to exit, not for the asynchronous
                // output handlers to drain. The parameterless overload does, and returns immediately
                // here because the process has already exited.
                process.WaitForExit();
            }
            else
            {
                timedOut = true;
                try
                {
                    process.Kill();
                    // Give the process a moment to actually die so we can read its exit code.
                    process.WaitForExit(1000);
                }
                catch (Exception)
                {
                    // Kill can fail if the process exited between the timeout and this call, or if
                    // the OS denies access. Either way there's nothing useful to do about it.
                }

                // Stop the readers rather than waiting for them, so a process that could not be
                // killed doesn't block this thread indefinitely.
                try
                {
                    process.CancelOutputRead();
                    process.CancelErrorRead();
                }
                catch (InvalidOperationException)
                {
                    // Reads were already stopped.
                }
            }

            var exitCode = process.HasExited ? process.ExitCode : -1;
            process.Close();

            lock (streamLock)
                return new ShellReturnInfo(startInfo, exitCode, output.ToString(), error.ToString());
        }
    }
}
