using System.Collections;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Unity.Android.Logcat;
using UnityEngine;
using UnityEngine.TestTools;

public class AndroidLogcatDispatcherTests
{
    internal struct TaskInputData : IAndroidLogcatTaskInput
    {
        internal int mainThreadId;
    }

    internal struct TaskResultData : IAndroidLogcatTaskResult
    {
        internal int workerThreadId;
        internal int mainThreadId;
    }

    IAndroidLogcatTaskResult PerformAsycnTask(IAndroidLogcatTaskInput input)
    {
        var data = (TaskInputData)input;
        return new TaskResultData() { mainThreadId = data.mainThreadId, workerThreadId = Thread.CurrentThread.ManagedThreadId };
    }

    void IntegrateTask(IAndroidLogcatTaskResult result)
    {
        Assert.Fail();
    }

    [UnityTest]
    public IEnumerator SimpleDispatchingWorks([Values(true, false)] bool synchronousTask)
    {
        var runtime = new AndroidLogcatTestRuntime();
        runtime.Initialize();

        bool taskFinished = false;
        TaskResultData result = new TaskResultData() { mainThreadId = 0, workerThreadId = 0 };
        TaskInputData data = new TaskInputData() { mainThreadId = Thread.CurrentThread.ManagedThreadId };

        runtime.Dispatcher.Schedule(data, PerformAsycnTask, (IAndroidLogcatTaskResult r) =>
        {
            taskFinished = true;
            result = (TaskResultData)r;
        }, synchronousTask);

        var startTime = Time.realtimeSinceStartup;
        const float kMaxWaitTime = 1.0f;
        do
        {
            runtime.Update();
            yield return null;
        }
        while (!taskFinished && Time.realtimeSinceStartup - startTime < kMaxWaitTime);

        Assert.IsTrue(taskFinished, string.Format("Timeout while waiting for task to be finished, waited {0} seconds", Time.realtimeSinceStartup - startTime));

        if (synchronousTask)
        {
            Assert.IsTrue(result.mainThreadId == result.workerThreadId && result.mainThreadId > 0 && result.workerThreadId > 0,
                string.Format("Expected main ({0}) and worker thread ({1}) to match and be bigger than 0", result.mainThreadId, result.workerThreadId));
        }
        else
        {
            Assert.IsTrue(result.mainThreadId != result.workerThreadId && result.mainThreadId > 0 && result.workerThreadId > 0,
                string.Format("Expected main ({0}) and worker thread ({1}) to not match and be bigger than 0", result.mainThreadId, result.workerThreadId));
        }

        runtime.Shutdown();
    }
}
