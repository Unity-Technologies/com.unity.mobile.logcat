using System;
using UnityEditor;

namespace Unity.Android.Logcat
{
    public static class AndroidLogcatTestExtension
    {
        public static string ToABI(this AndroidArchitecture androidArchitecture)
        {
            return androidArchitecture switch
            {
                AndroidArchitecture.ARMv7 => "armeabi-v7a",
                AndroidArchitecture.ARM64 => "arm64-v8a",
                _ => throw new NotImplementedException(androidArchitecture.ToString()),
            };
        }

        public static string ToNdkArchitecture(this AndroidArchitecture androidArchitecture)
        {
            return androidArchitecture switch
            {
                AndroidArchitecture.ARMv7 => "arm",
                AndroidArchitecture.ARM64 => "arm64",
                _ => throw new NotImplementedException(androidArchitecture.ToString()),
            };
        }
    }
}
