#if PLATFORM_ANDROID
using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;
using System.Threading;
using UnityEngine.Profiling;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatDispatcher : ScriptableSingleton<AndroidLogcatDispatcher>
    {
        private class IntegrationTask
        {
            internal AndroidLogcatTaskResult result;
            internal Action<AndroidLogcatTaskResult> integrateAction;
        }

        private class AsyncTask
        {
            internal AndroidLogcatTaskInput taskData;
            internal Func<AndroidLogcatTaskInput, AndroidLogcatTaskResult> asyncAction;
            internal Action<AndroidLogcatTaskResult> integrateAction;
        }

        CustomSampler m_Sampler;

        private Queue<AsyncTask> m_AsyncTaskQueue = new Queue<AsyncTask>();
        private Queue<IntegrationTask> m_IntegrateTaskQueue = new Queue<IntegrationTask>();
        private AutoResetEvent m_AutoResetEvent = new AutoResetEvent(false);
        private AutoResetEvent m_FinishedEvent = new AutoResetEvent(false);
        private volatile bool m_Running;
        private static Thread s_MainThread;

        internal void OnEnable()
        {
            if (m_Running)
                throw new Exception("Already running?");
            m_Running = true;

            lock (m_AsyncTaskQueue)
                m_AsyncTaskQueue.Clear();

            lock (m_IntegrateTaskQueue)
                m_IntegrateTaskQueue.Clear();

            EditorApplication.update += MainThread;
            ThreadPool.QueueUserWorkItem(WorkerThread);

            m_Sampler = CustomSampler.Create("AndroidLogcat Async Work");

            s_MainThread = Thread.CurrentThread;
        }

        internal void OnDisable()
        {
            if (!m_Running)
                throw new Exception("Expected dispatcher to run");
            EditorApplication.update -= MainThread;
            m_Running = false;
            m_AutoResetEvent.Set();
            if (!m_FinishedEvent.WaitOne(1000))
                throw new Exception("Time out while waiting for android logcat dispatcher to exit.");

            lock (m_AsyncTaskQueue)
                m_AsyncTaskQueue.Clear();

            lock (m_IntegrateTaskQueue)
                m_IntegrateTaskQueue.Clear();

            AndroidLogcatInternalLog.Log("Dispatcher shutting down");
        }

        internal static bool isMainThread
        {
            get
            {
                return Thread.CurrentThread == s_MainThread;
            }
        }

        private void WorkerThread(object o)
        {
            AndroidLogcatInternalLog.Log("Worker thread started");
            Profiler.BeginThreadProfiling("AndroidLogcat", "Dispatcher");

            while (m_AutoResetEvent.WaitOne() && m_Running)
            {
                //Debug.Log("Executing");
                AsyncTask task = null;
                lock (m_AsyncTaskQueue)
                {
                    if (m_AsyncTaskQueue.Count > 0)
                    {
                        task = m_AsyncTaskQueue.Dequeue();
                    }
                }
                if (task != null && task.asyncAction != null)
                {
                    m_Sampler.Begin();
                    var result = task.asyncAction.Invoke(task.taskData);
                    m_Sampler.End();

                    lock (m_IntegrateTaskQueue)
                    {
                        m_IntegrateTaskQueue.Enqueue(new IntegrationTask() { integrateAction = task.integrateAction, result = result });
                    }
                }
            }
            AndroidLogcatInternalLog.Log("Worker thread exited");
            Profiler.EndThreadProfiling();
            m_FinishedEvent.Set();
        }

        private void MainThread()
        {
            var temp = new Queue<IntegrationTask>();

            lock (m_IntegrateTaskQueue)
            {
                foreach (var i in m_IntegrateTaskQueue)
                    temp.Enqueue(i);
                m_IntegrateTaskQueue.Clear();
            }

            foreach (var t in temp)
            {
                //Debug.Log("Integrating");
                t.integrateAction.Invoke(t.result);
            }
        }

        internal void Schedule(AndroidLogcatTaskInput taskData, Func<AndroidLogcatTaskInput, AndroidLogcatTaskResult> asyncAction, Action<AndroidLogcatTaskResult> integrateAction, bool immediate)
        {
            if (!m_Running)
            {
                AndroidLogcatInternalLog.Log("Ignore schedule action, because dispatcher is not running.");
                return;
            }

            if (immediate)
            {
                integrateAction(asyncAction.Invoke(taskData));
                return;
            }

            lock (m_AsyncTaskQueue)
            {
                var task = new AsyncTask() { taskData = taskData, asyncAction = asyncAction, integrateAction = integrateAction };
                m_AsyncTaskQueue.Enqueue(task);
                if (!m_AutoResetEvent.Set())
                    throw new Exception("Failed to signal auto reset event in dispatcher.");
            }
        }
    }
}
#endif
