#if PLATFORM_ANDROID
using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;
using System.Threading;

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

        private Queue<AsyncTask> m_AsyncTaskQueue = new Queue<AsyncTask>();
        private Queue<IntegrationTask> m_IntegrateTaskQueue = new Queue<IntegrationTask>();
        private AutoResetEvent m_AutoResetEvent = new AutoResetEvent(false);
        private AutoResetEvent m_FinishedEvent = new AutoResetEvent(false);
        private volatile bool m_Running;

        internal void OnEnable()
        {
            if (m_Running)
                throw new Exception("Already running?");
            m_Running = true;
            EditorApplication.update += MainThread;
            ThreadPool.QueueUserWorkItem(WorkerThread);
        }

        internal void OnDisable()
        {
            if (!m_Running)
                throw new Exception("Expected dispatcher to run");
            EditorApplication.update -= MainThread;
            m_Running = false;
            m_AutoResetEvent.Set();
            m_FinishedEvent.WaitOne();
            Debug.Log("Dispatcher shutting down");
        }

        private void WorkerThread(object o)
        {
            Debug.Log("Worker thread started");

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
                    var result = task.asyncAction.Invoke(task.taskData);

                    lock (m_IntegrateTaskQueue)
                    {
                        m_IntegrateTaskQueue.Enqueue(new IntegrationTask() { integrateAction = task.integrateAction, result = result });
                    }
                }
            }
            Debug.Log("Worker thread exited");
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
            if (immediate)
            {
                integrateAction(asyncAction.Invoke(taskData));
                return;
            }

            lock (m_AsyncTaskQueue)
            {
                var task = new AsyncTask() { taskData = taskData, asyncAction = asyncAction, integrateAction = integrateAction };
                m_AsyncTaskQueue.Enqueue(task);
                m_AutoResetEvent.Set();
            }
        }
    }
}
#endif
