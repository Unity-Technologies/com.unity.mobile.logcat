using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Object = System.Object;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Provides dynamic way of accessing UnityEditor.Android.Extensions.dll.
    /// It solves the problem where if you have logcat package installed, but there's no Android Support installed, you won't get compiler errors
    /// It also enables to use Logcat package when active platform is not Android
    /// </summary>
    class AndroidBridge
    {
        enum ExtensionState
        {
            Undefined,
            Unavalaible,
            Available
        }

        private static ExtensionState s_AndroidExtensionsState = ExtensionState.Undefined;
        private static Assembly s_AndroidExtensions;
        private static readonly string kAndroidLogcatWarningIssued = nameof(kAndroidLogcatWarningIssued);

        private static Assembly AndroidExtensions
        {
            get
            {
                // Fast exit, since reflection is very slow
                if (s_AndroidExtensionsState == ExtensionState.Unavalaible)
                    return null;

                if (s_AndroidExtensions != null)
                    return s_AndroidExtensions;
                var assemblyName = "UnityEditor.Android.Extensions";
                s_AndroidExtensions = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.FullName.Contains(assemblyName));
                s_AndroidExtensionsState = s_AndroidExtensions == null ? ExtensionState.Unavalaible : ExtensionState.Available;

                // Warn user once why logcat is disabled
                if (SessionState.GetBool(kAndroidLogcatWarningIssued, false) == false &&
                    s_AndroidExtensionsState == ExtensionState.Unavalaible)
                {
                    SessionState.SetBool(kAndroidLogcatWarningIssued, true);
                    Debug.LogWarning($"{assemblyName} assembly not found, android logcat will be disabled.");
                }

                return s_AndroidExtensions;
            }
        }

        internal static bool AndroidExtensionsInstalled => AndroidExtensions != null;

        internal class ADB
        {
            private static Type s_ADBType;
            private static MethodInfo s_GetInstanceMethodInfo;
            private static MethodInfo s_GetADBPathMethodInfo;
            private static MethodInfo s_RunMethodInfo;

            private readonly System.Object m_ADBObject;

            private static Type UnderlyingType
            {
                get
                {
                    if (s_ADBType != null)
                        return s_ADBType;
                    if (AndroidExtensions == null)
                        return null;
                    s_ADBType = AndroidExtensions.GetType("UnityEditor.Android.ADB");
                    if (s_ADBType == null)
                        throw new Exception("Failed to locate ADB type");
                    return s_ADBType;
                }
            }

            internal System.Object UnderlyingObject => m_ADBObject;

            private static MethodInfo GetInstanceMethodInfo
            {
                get
                {
                    if (s_GetInstanceMethodInfo != null)
                        return s_GetInstanceMethodInfo;
                    s_GetInstanceMethodInfo = UnderlyingType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static);
                    return s_GetInstanceMethodInfo;
                }
            }

            private static MethodInfo GetADBPathMethodInfo
            {
                get
                {
                    if (s_GetADBPathMethodInfo != null)
                        return s_GetADBPathMethodInfo;
                    s_GetADBPathMethodInfo = UnderlyingType.GetMethod("GetADBPath", BindingFlags.Public | BindingFlags.Instance);
                    return s_GetADBPathMethodInfo;
                }
            }

            private static MethodInfo RunMethodInfo
            {
                get
                {
                    if (s_RunMethodInfo != null)
                        return s_RunMethodInfo;
                    s_RunMethodInfo = UnderlyingType.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance);
                    return s_RunMethodInfo;
                }
            }

            private ADB(System.Object adbObject)
            {
                if (adbObject == null)
                    throw new ArgumentNullException("ADB instance cannot be null. Is Android SDK set?");
                m_ADBObject = adbObject;
            }

            public string GetADBPath()
            {
                return (string)GetADBPathMethodInfo.Invoke(m_ADBObject, null);
            }

            public string Run(string[] command, string errorMsg)
            {
                return (string)RunMethodInfo.Invoke(m_ADBObject, new Object[] { command, errorMsg });
            }

            public static ADB GetInstance()
            {
                return new ADB(GetInstanceMethodInfo.Invoke(null, null));
            }
        }

        internal class AndroidDevice
        {
            private static Type s_AndroidDeviceType;
            private static PropertyInfo s_PropertiesPropertyInfo;

            private System.Object m_AndroidDeviceObject;

            internal class PropertiesTable
            {
                private PropertyInfo m_GetItemPropertyInfo;
                private System.Object m_PropertiesTableObject;

                internal PropertiesTable(System.Object propertiesTableObject)
                {
                    m_PropertiesTableObject = propertiesTableObject;
                    m_GetItemPropertyInfo = m_PropertiesTableObject.GetType().GetProperty("Item");
                }

                public string this[string key]
                {
                    get
                    {
                        return (string)m_GetItemPropertyInfo.GetValue(m_PropertiesTableObject, new Object[] { key });
                    }
                }
            }

            private static Type UnderlyingType
            {
                get
                {
                    if (s_AndroidDeviceType != null)
                        return s_AndroidDeviceType;
                    if (AndroidExtensions == null)
                        return null;
                    s_AndroidDeviceType = AndroidExtensions.GetType("UnityEditor.Android.AndroidDevice");
                    if (s_AndroidDeviceType == null)
                        throw new Exception("Failed to locate AndroidDevice type");
                    return s_AndroidDeviceType;
                }
            }

            private static PropertyInfo PropertiesPropertyInfo
            {
                get
                {
                    if (s_PropertiesPropertyInfo != null)
                        return s_PropertiesPropertyInfo;
                    s_PropertiesPropertyInfo = UnderlyingType.GetProperty("Properties");
                    return s_PropertiesPropertyInfo;
                }
            }

            public AndroidDevice(ADB adb, string deviceId)
            {
                m_AndroidDeviceObject = Activator.CreateInstance(UnderlyingType, adb.UnderlyingObject, deviceId);
            }

            public PropertiesTable Properties => new PropertiesTable(PropertiesPropertyInfo.GetValue(m_AndroidDeviceObject, null));
        }

        internal class AndroidExternalToolsSettings
        {
            private static Type s_AndroidExternalToolsSettingsType;
            private static PropertyInfo s_NdkRootPathProperty;
            private static PropertyInfo s_SdkRootPathProperty;

            private static Type UnderlyingType
            {
                get
                {
                    if (s_AndroidExternalToolsSettingsType != null)
                        return s_AndroidExternalToolsSettingsType;
                    if (AndroidExtensions == null)
                        return null;
                    s_AndroidExternalToolsSettingsType = AndroidExtensions.GetType("UnityEditor.Android.AndroidExternalToolsSettings");
                    if (s_AndroidExternalToolsSettingsType == null)
                        throw new Exception("Failed to locate ADB ndroidExternalToolsSettings");

                    return s_AndroidExternalToolsSettingsType;
                }
            }

            private static PropertyInfo NdkRootPathProperty
            {
                get
                {
                    if (s_NdkRootPathProperty != null)
                        return s_NdkRootPathProperty;
                    s_NdkRootPathProperty = UnderlyingType.GetProperty("ndkRootPath");
                    return s_NdkRootPathProperty;
                }
            }

            private static PropertyInfo SdkRootPathProperty
            {
                get
                {
                    if (s_SdkRootPathProperty != null)
                        return s_SdkRootPathProperty;
                    s_SdkRootPathProperty = UnderlyingType.GetProperty("sdkRootPath");
                    return s_SdkRootPathProperty;
                }
            }

            /// <summary>
            /// Matches to UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath
            /// </summary>
            public static string ndkRootPath
            {
                get => (string)NdkRootPathProperty.GetValue(null);
                set => NdkRootPathProperty.SetValue(null, value);
            }

            /// <summary>
            /// /// Matches to UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath
            /// </summary>
            public static string sdkRootPath
            {
                get => (string)SdkRootPathProperty.GetValue(null);
                set => SdkRootPathProperty.SetValue(null, value);
            }
        }
    }
}
