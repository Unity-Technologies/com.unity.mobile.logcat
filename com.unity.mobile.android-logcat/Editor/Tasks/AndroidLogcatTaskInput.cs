namespace Unity.Android.Logcat
{
    internal interface IAndroidLogcatTaskInput
    {
    }

    internal class AndroidLogcatTaskInput<T> : IAndroidLogcatTaskInput
    {
        internal T data;
    }

    internal class AndroidLogcatTaskInput<T1, T2> : IAndroidLogcatTaskInput
    {
        internal T1 data1;
        internal T2 data2;
    }
    internal class AndroidLogcatTaskInput<T1, T2, T3> : IAndroidLogcatTaskInput
    {
        internal T1 data1;
        internal T2 data2;
        internal T3 data3;
    }
}
