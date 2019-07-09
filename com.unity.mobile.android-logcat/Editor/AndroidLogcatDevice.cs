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

        internal abstract string OSVersion { get; }

        internal abstract string ABI { get; }

        internal abstract string Id { get; }

        // Check if it is Android 7 or above due to the below options are only available on these devices:
        // 1) '--pid'
        // 2) 'logcat -v year'
        // 3) '--regex'
        internal bool IsAndroid7orAbove { get { return SDKVersion >= 24; } }
    }

    internal class AndroidLogcatDevice : IAndroidLogcatDevice
    {
        private AndroidDevice m_Device;

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

        internal override string OSVersion
        {
            get { return m_Device.Properties["ro.build.version.release"]; }
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
