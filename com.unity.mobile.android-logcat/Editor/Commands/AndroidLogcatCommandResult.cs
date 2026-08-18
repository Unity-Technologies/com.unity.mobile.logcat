using System;
using System.Reflection;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Outcome of running a single command, passed back to the main thread by the dispatcher.
    /// </summary>
    internal class AndroidLogcatCommandResult : IAndroidLogcatTaskResult
    {
        internal string Command { get; private set; }
        internal string Output { get; private set; }
        internal string Error { get; private set; }

        internal bool Failed => !string.IsNullOrEmpty(Error);

        AndroidLogcatCommandResult(string command, string output, string error)
        {
            Command = command;
            Output = output;
            Error = error;
        }

        internal static AndroidLogcatCommandResult CreateSuccess(string command, string output)
        {
            return new AndroidLogcatCommandResult(command, output, null);
        }

        internal static AndroidLogcatCommandResult CreateFailure(string command, string error)
        {
            return new AndroidLogcatCommandResult(command, null, string.IsNullOrEmpty(error) ? "Unknown error." : error);
        }

        /// <summary>
        /// ADB is invoked through reflection, so a failure surfaces as a TargetInvocationException
        /// whose message says nothing useful. Unwrap it to recover adb's actual error.
        /// </summary>
        internal static Exception Unwrap(Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null)
                ex = ex.InnerException;
            return ex;
        }
    }

    /// <summary>
    /// Runs non-adb commands (for example bundletool) on the dispatcher's worker thread.
    /// adb commands go through <see cref="IAndroidLogcatDevice.RunAdbCommandAsync"/> instead.
    /// </summary>
    internal static class AndroidLogcatHostCommand
    {
        /// <summary>
        /// Commands entered by the user may never exit on their own, so they are always bounded.
        /// </summary>
        internal const int kTimeoutMs = 60 * 1000;

        internal static void RunAsync(AndroidLogcatDispatcher dispatcher, string command, Action<AndroidLogcatCommandResult> onComplete)
        {
            if (dispatcher == null)
            {
                onComplete?.Invoke(AndroidLogcatCommandResult.CreateFailure(command, "Dispatcher is not available."));
                return;
            }

            if (!AndroidLogcatCommandParser.TrySplitExecutable(command, out var executable, out var arguments))
            {
                onComplete?.Invoke(AndroidLogcatCommandResult.CreateFailure(command, "Could not determine which executable to run."));
                return;
            }

            dispatcher.Schedule(
                new AndroidLogcatTaskInput<string, string, string>()
                {
                    data1 = executable,
                    data2 = arguments,
                    data3 = command
                },
                (input) =>
                {
                    var inputData = (AndroidLogcatTaskInput<string, string, string>)input;
                    AndroidLogcatInternalLog.Log($"{inputData.data1} {inputData.data2}");
                    try
                    {
                        var shellResult = Shell.RunProcess(inputData.data1, inputData.data2, kTimeoutMs, out var timedOut);
                        var output = shellResult.GetStandardOut();
                        var error = shellResult.GetStandardErr();

                        if (timedOut)
                        {
                            return AndroidLogcatCommandResult.CreateFailure(inputData.data3,
                                $"Command did not finish within {kTimeoutMs / 1000} seconds and was terminated.\n{output}{error}");
                        }

                        if (!string.IsNullOrEmpty(error) && shellResult.GetExitCode() != 0)
                            return AndroidLogcatCommandResult.CreateFailure(inputData.data3, error);

                        // Non-zero stderr with a zero exit code is usually warnings, keep it in the output.
                        if (!string.IsNullOrEmpty(error))
                            output = string.IsNullOrEmpty(output) ? error : output + "\n" + error;

                        return AndroidLogcatCommandResult.CreateSuccess(inputData.data3, output);
                    }
                    catch (Exception ex)
                    {
                        return AndroidLogcatCommandResult.CreateFailure(inputData.data3, AndroidLogcatCommandResult.Unwrap(ex).Message);
                    }
                },
                (result) => onComplete?.Invoke((AndroidLogcatCommandResult)result),
                false);
        }
    }
}
