#if PLATFORM_ANDROID
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Android;
using System.Text;


namespace Unity.Android.Logcat
{
    internal abstract class IAndroidLogcatDevice
    {
        internal abstract int SDKVersion { get; }

        internal abstract string Manufacturer { get; }

        internal abstract string Model { get; }

        internal abstract Version OSVersion { get; }

        internal abstract string ABI { get; }

        internal abstract string Id { get; }
    }

    internal class AndroidLogcatDevice : IAndroidLogcatDevice
    {
        private AndroidDevice m_Device;
        private Version m_Version;

        internal AndroidLogcatDevice(AndroidDevice device)
        {
            m_Device = device;
        }

        internal override int SDKVersion
        {
            get { return int.Parse(m_Device.Properties["ro.build.version.sdk"]); }
        }

        internal override string Manufacturer
        {
            get { return m_Device.Properties["ro.product.manufacturer"]; }
        }

        internal override string Model
        {
            get { return m_Device.Properties["ro.product.model"]; }
        }

        internal override Version OSVersion
        {
            get
            {
                if (m_Version == null)
                {
                    var versionString = m_Device.Properties["ro.build.version.release"];
                    if (!Version.TryParse(versionString, out m_Version))
                    {
                        AndroidLogcatInternalLog.Log("Failed to parse android OS version '{0}'", versionString);
                        m_Version = new Version(0, 0);
                    }
                }

                return m_Version;
            }
        }

        internal override string ABI
        {
            get { return m_Device.Properties["ro.product.cpu.abi"]; }
        }

        internal override string Id
        {
            get { return m_Device.Id; }
        }
    }
}
#endif
