using System;
using System.IO;
using Unity.Android.Logcat;

internal class AndroidLogcatRuntimeIntegrationTestBase
{
    protected AndroidLogcatRuntime m_Runtime;

    protected void Cleanup()
    {
        if (Directory.Exists("Tests"))
            Directory.Delete("Tests", true);
    }

    protected void InitRuntime(bool cleanup = true)
    {
        if (m_Runtime != null)
            throw new Exception("Runtime was not shutdown by previous test?");
        m_Runtime = new AndroidLogcatRuntime(false);
        if (cleanup)
            Cleanup();
        m_Runtime.Initialize();
    }

    protected void ShutdownRuntime(bool cleanup = true)
    {
        if (m_Runtime == null)
            throw new Exception("Runtime was not created?");
        m_Runtime.Shutdown();
        if (cleanup)
            Cleanup();
        m_Runtime = null;
    }
}
