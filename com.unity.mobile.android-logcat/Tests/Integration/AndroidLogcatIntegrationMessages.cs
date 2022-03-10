using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Android.Logcat;
using UnityEngine.TestTools;

[TestFixture]
[RequiresAndroidDevice]
internal class AndroidLogcatRuntimeIntegrationMessages : AndroidLogcatIntegrationTestBase
{
    [UnityTest]
    public IEnumerator CanGetMessagesFromDevice()
    {
        var logcat = CreateLogcatInstance();
        var messageCount = 0;
        var lastMessage = string.Empty;
        logcat.FilteredLogEntriesAdded += (IReadOnlyList<LogcatEntry> e) =>
        {
            if (e.Count == 0)
                return;
            messageCount++;
            lastMessage = e[e.Count - 1].message;
        };
        logcat.Start();

        yield return WaitForCondition("Waiting for messages", () => messageCount > 0);

        Log($"Received {messageCount} messages, last message: '{lastMessage}'");

        logcat.Stop();
    }
}
