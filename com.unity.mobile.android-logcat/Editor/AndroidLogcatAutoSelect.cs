using System;
using UnityEditor;
namespace Unity.Android.Logcat
{
    internal class AutoSelect
    {
        private const int kMillisecondsBetweenConsecutiveAutoConnectChecks = 1000;
        private const int kMillisecondsMaxAutoconnectTimeOut = 5000;

        public enum State
        {
            Idle,
            InProgress
        }

        private State m_State;
        private DateTime m_TimeOfLastAutoConnectUpdate;
        private DateTime m_TimeOfLastAutoConnectStart;

        public string PackageName { private set; get; }
        public string DeviceId { private set; get; }

        public State GetState()
        {
            return m_State;
        }

        public AutoSelect()
        {
            Finish();
        }

        public void Start(string deviceId, string package)
        {
            m_TimeOfLastAutoConnectStart = DateTime.Now;
            m_State = State.InProgress;
            DeviceId = deviceId;
            PackageName = package;
        }

        public bool ShouldTick()
        {
            // This is for AutoRun triggered by "Build And Run".
            if ((DateTime.Now - m_TimeOfLastAutoConnectUpdate).TotalMilliseconds < kMillisecondsBetweenConsecutiveAutoConnectChecks)
                return false;
            AndroidLogcatInternalLog.Log($"Waiting for {PackageName} launch, elapsed {(DateTime.Now - m_TimeOfLastAutoConnectStart).Seconds} seconds");
            m_TimeOfLastAutoConnectUpdate = DateTime.Now;
            return true;
        }

        public double CheckTimeout()
        {
            var timeoutMS = (DateTime.Now - m_TimeOfLastAutoConnectStart).TotalMilliseconds;
            if (timeoutMS > kMillisecondsMaxAutoconnectTimeOut)
                return timeoutMS;
            return -1;
        }

        public void Finish()
        {
            m_State = State.Idle;
            DeviceId = string.Empty;
            PackageName = string.Empty;
            m_TimeOfLastAutoConnectUpdate = DateTime.MinValue;
            m_TimeOfLastAutoConnectStart = DateTime.MinValue;
        }

        public void Reset()
        {
            Finish();
        }

        public override string ToString()
        {
            return $"State: {m_State}, DeviceId: {DeviceId}, Package: {PackageName}";
        }
    }
}
