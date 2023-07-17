using System;
using System.Collections.Generic;
using UnityEditor;
using System.Text;
using UnityEngine;

namespace Unity.Android.Logcat
{
    internal enum ExtraWindow
    {
        Hidden,
        Memory,
        Inputs
    }

    [Serializable]
    internal class ExtraWindowState
    {
        public ExtraWindow Type = ExtraWindow.Hidden;
        public float Height;
    }
}
