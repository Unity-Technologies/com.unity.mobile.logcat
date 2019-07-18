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
        // Check if it is Android 7 or above due to the below options are only available on these devices:
        // 1) '--pid'
        // 2) 'logcat -v year'
        // 3) '--regex'
        internal static readonly Version kAndroidVersion70 = new Version(7, 0);

        internal abstract int APILevel { get; }

        internal abstract string Manufacturer { get; }

        internal abstract string Model { get; }

        internal abstract Version OSVersion { get; }

        internal abstract string ABI { get; }

        internal abstract string Id { get; }

        internal bool SupportsFilteringByRegex
        {
            get { return OSVersion >= kAndroidVersion70; }
        }

        internal bool SupportsFilteringByPid
        {
            get { return OSVersion >= kAndroidVersion70; }
        }

        internal bool SupportYearFormat
        {
            get { return OSVersion >= kAndroidVersion70; }
        }
    }

    internal class AndroidLogcatDevice : IAndroidLogcatDevice
    {
        private AndroidDevice m_Device;
        private Version m_Version;

        internal AndroidLogcatDevice(AndroidDevice device)
        {
            m_Device = device;
        }

        internal override int APILevel
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
#if NET_2_0
                    int major = 0;
                    int minor = 0;
                    int build = 0;
                    var vals = versionString.Split('.');
                    if (vals.Length > 0)
                        int.TryParse(vals[0], out major);
                    if (vals.Length > 1)
                        int.TryParse(vals[1], out minor);
                    if (vals.Length > 2)
                        int.TryParse(vals[2], out build);

                    m_Version = new Version(major, minor, build);
#else
                    if (!Version.TryParse(versionString, out m_Version))
                    {
                        AndroidLogcatInternalLog.Log("Failed to parse android OS version '{0}'", versionString);
                        m_Version = new Version(0, 0);
                    }
#endif
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
