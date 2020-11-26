using System.Diagnostics;
using System;

namespace Unity.Android.Logcat
{
    internal abstract class AndroidLogcatMessageProviderBase
    {
        protected AndroidBridge.ADB m_ADB;
        protected string m_Filter;
        protected AndroidLogcat.Priority m_Priority;
        protected int m_PackageID;
        protected string m_LogPrintFormat;
        protected IAndroidLogcatDevice m_Device;
        protected Action<string> m_LogCallbackAction;

        internal AndroidLogcatMessageProviderBase(AndroidBridge.ADB adb, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, IAndroidLogcatDevice device, Action<string> logCallbackAction)
        {
            m_ADB = adb;
            m_Filter = filter;
            m_Priority = priority;
            m_PackageID = packageID;
            m_LogPrintFormat = logPrintFormat;
            m_Device = device;
            m_LogCallbackAction = logCallbackAction;

            if (device != null && !device.SupportsFilteringByRegex && !string.IsNullOrEmpty(m_Filter))
                throw new Exception($"Device '{device.Id}' doesn't support filtering by regex, by filter was specified?");
        }

        public abstract void Start();
        public abstract void Stop();
        public abstract void Kill();
        public abstract bool HasExited { get; }
    }

    internal class AndroidLogcatMessageProvider : AndroidLogcatMessageProviderBase
    {
        private Process m_LogcatProcess;

        internal AndroidLogcatMessageProvider(AndroidBridge.ADB adb, string filter, AndroidLogcat.Priority priority, int packageID, string logPrintFormat, IAndroidLogcatDevice device, Action<string> logCallbackAction)
            : base(adb, filter, priority, packageID, logPrintFormat, device, logCallbackAction)
        {
        }

        private string PriorityEnumToString(AndroidLogcat.Priority priority)
        {
            return priority.ToString().Substring(0, 1);
        }

        private string LogcatArguments()
        {
            var filterArg = string.Empty;
            if (!string.IsNullOrEmpty(m_Filter))
                filterArg = "--regex \"" + m_Filter + "\"";

            // Note: We're not using --regex argument, because some older Android device (prior to 7.0) doesn't support that
            var priority = PriorityEnumToString(m_Priority);
            if (m_PackageID > 0)
                return string.Format("-s {0} logcat --pid={1} -v {2} *:{3} {4}", m_Device.Id, m_PackageID, m_LogPrintFormat, priority, filterArg);

            return string.Format("-s {0} logcat -v {1} *:{2} {3}", m_Device.Id, m_LogPrintFormat, priority, filterArg);
        }

        public override void Start()
        {
            var arguments = LogcatArguments();
            AndroidLogcatInternalLog.Log("\n\nStarting logcat\n\n");
            AndroidLogcatInternalLog.Log("{0} {1}", m_ADB.GetADBPath(), arguments);
            m_LogcatProcess = new Process();
            m_LogcatProcess.StartInfo.FileName = m_ADB.GetADBPath();
            m_LogcatProcess.StartInfo.Arguments = arguments;
            m_LogcatProcess.StartInfo.RedirectStandardError = true;
            m_LogcatProcess.StartInfo.RedirectStandardOutput = true;
            m_LogcatProcess.StartInfo.UseShellExecute = false;
            m_LogcatProcess.StartInfo.CreateNoWindow = true;
            m_LogcatProcess.OutputDataReceived += OutputDataReceived;
            m_LogcatProcess.ErrorDataReceived += OutputDataReceived;
            m_LogcatProcess.Start();

            m_LogcatProcess.BeginOutputReadLine();
            m_LogcatProcess.BeginErrorReadLine();
        }

        public override void Stop()
        {
            if (m_LogcatProcess != null && !m_LogcatProcess.HasExited)
                m_LogcatProcess.Kill();

            m_LogcatProcess = null;
        }

        public override void Kill()
        {
            // NOTE: DONT CALL CLOSE, or ADB process will stay alive all the time
            AndroidLogcatInternalLog.Log("Stopping logcat (process id {0})", m_LogcatProcess.Id);
            m_LogcatProcess.Kill();
        }

        public override bool HasExited
        {
            get
            {
                return m_LogcatProcess.HasExited;
            }
        }

        private void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            m_LogCallbackAction(e.Data);
        }
    }
}
