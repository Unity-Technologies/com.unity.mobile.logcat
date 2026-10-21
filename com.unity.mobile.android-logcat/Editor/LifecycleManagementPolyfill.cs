#if !UNITY_7000_0_OR_NEWER
using System;

namespace Unity.Scripting.LifecycleManagement
{
    /// <summary>
    /// Polyfill for Unity versions without the Lifecycle Management APIs, so [NoAutoStaticsCleanup] can be used unconditionally.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    internal sealed class NoAutoStaticsCleanupAttribute : Attribute
    {
    }
}
#endif
