using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Profiling;

public class MemoryAllocations : MonoBehaviour
{
    enum MemoryType
    {
        Native,
        Java,
        Graphics,
        Managed
    }

    const int kOneMB = 1000 * 1000;
    const int kAllocationUnit = 10 * 1000 * 1000;
    const int kAllocationUnityInMb = kAllocationUnit / 1000000;
    struct NativeMemoryData
    {
        internal IntPtr pointer;
        internal int size;
    }

    List<NativeMemoryData> m_NativeAllocations = new List<NativeMemoryData>();
    List<Texture2D> m_Textures = new List<Texture2D>();
    List<RenderTexture> m_RenderTextures = new List<RenderTexture>();
    List<byte[]> m_ManagedMemory = new List<byte[]>();

    AndroidJavaObject m_JavaClass;
    MemoryType m_MemoryType;

    GUIContent[] m_ToolbarItems = ((MemoryType[])Enum.GetValues(typeof(MemoryType))).Select(m => new GUIContent(m.ToString())).ToArray();

#if UNITY_IOS
    [DllImport("__Internal")]
    private static extern int _iOSGetTotalNativeMemoryMB();
#endif

    void Start()
    {
        m_JavaClass = new AndroidJavaObject("com.unity3d.player.JavaMemory");
        if (m_JavaClass == null)
            throw new Exception("Failed to find com.unity3d.player.JavaMemory");

        Application.lowMemory += () => Debug.Log("Application.lowMemory called");
        Application.memoryUsageChanged += (in ApplicationMemoryUsageChange usage) => Debug.Log($"Application.memoryUsageChanged called with usage: {usage}");
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void OnDisable()
    {
        Debug.Log("Cleaning up");
        ClearNativeMemory();
    }

    private void ClearNativeMemory()
    {
        foreach (var native in m_NativeAllocations)
            Marshal.FreeHGlobal(native.pointer);
        m_NativeAllocations.Clear();
    }

    private int GetAllocatedNativeMemoryMB()
    {
        int total = 0;
        foreach (var native in m_NativeAllocations)
        {
            total += native.size;
        }
        return total / 1000000;
    }

    private void AllocateJavaMemory(int bytes)
    {
        var result = m_JavaClass.CallStatic<int>("allocateMemory", bytes);
        Debug.Log("Total java memory allocated " + result);
    }

    private void DeallocateJavaMemory()
    {
        m_JavaClass.CallStatic("deallocateMemory");
    }

    private void DirtyJavaMemory()
    {
        m_JavaClass.CallStatic("dirtyMemory");
    }

    private void ClearJavaMemory()
    {
        m_JavaClass.CallStatic("clearMemory");
    }

    private int GetAllocatedJavaMemoryMB()
    {
        return m_JavaClass.CallStatic<int>("getTotalMemoryMB");
    }

    void DoNativeGUI()
    {
        var message = $"Native Memory allocated {GetAllocatedNativeMemoryMB()} MB";
#if UNITY_IOS
        message += $", ObjC: {_iOSGetTotalNativeMemoryMB()}";
#endif
        GUILayout.Label(message);
        if (GUILayout.Button($"Allocate {kAllocationUnityInMb}MB of Native Memory"))
        {
            m_NativeAllocations.Add(new NativeMemoryData()
            {
                pointer = Marshal.AllocHGlobal(kAllocationUnit),
                size = kAllocationUnit
            });
        }

        GUILayout.Space(20);
        if (GUILayout.Button($"Deallocate {kAllocationUnityInMb}MB of Native Memory"))
        {
            if (m_NativeAllocations.Count > 0)
            {
                Marshal.FreeHGlobal(m_NativeAllocations[0].pointer);
                m_NativeAllocations.RemoveAt(0);
            }
        }
        GUILayout.Space(20);

        if (GUILayout.Button($"Dirty Native Memory"))
        {
            foreach (var n in m_NativeAllocations)
            {
                for (int i = 0; i < n.size - sizeof(UInt64); i += sizeof(UInt64))
                {
                    Marshal.WriteInt64(n.pointer, i, 1234);
                }
            }
        }
        GUILayout.Space(20);

        if (GUILayout.Button("Clear Native Memory"))
        {
            ClearNativeMemory();
        }
    }

    void DoJavaGUI()
    {
        GUILayout.Label($"Java Memory allocated {GetAllocatedJavaMemoryMB()} MB");
        if (GUILayout.Button($"Allocate {kAllocationUnityInMb}MB of Java Memory"))
            AllocateJavaMemory(kAllocationUnit);

        GUILayout.Space(20);
        if (GUILayout.Button($"Deallocate {kAllocationUnityInMb}MB of Java Memory"))
        {
            DeallocateJavaMemory();
        }
        GUILayout.Space(20);

        if (GUILayout.Button($"Dirty Java Memory"))
        {
            DirtyJavaMemory();
        }
        GUILayout.Space(20);

        if (GUILayout.Button("Clear Java Memory"))
        {
            ClearJavaMemory();
        }
    }

    void DoGraphicsGUI()
    {
        int width = 1024;
        int height = 1024;
        int depth = 16;

        int size = 0;
        for (int i = 0; i < m_Textures.Count; i++)
        {
            size += width * height * 4;
        }

        int renderTexturesSize = 0;
        for (int i = 0; i < m_RenderTextures.Count; i++)
        {
            renderTexturesSize += width * height * 4 * depth / 8;
        }

        GUILayout.Label($"Texture2D Memory allocated {size / 1000000} MB");
        GUILayout.Label($"RenderTexture Memory allocated {renderTexturesSize / 1000000} MB");
        if (GUILayout.Button($"Allocate Texture2D {4 * width * height / 1000000} MB"))
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int i = 0; i < width * height; i++)
            {
                var val = (byte)(i % 255);
                pixels[i] = new Color32(val, val, val, 255);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            m_Textures.Add(texture);
        }

        GUILayout.Space(20);

        if (GUILayout.Button("Clear Texture2D Memory"))
        {
            m_Textures.Clear();
            GC.Collect();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        GUILayout.Space(20);
        if (GUILayout.Button($"Allocate RenderTexture {depth / 8 * 4 * width * height / 1000000} MB"))
        {
            var r = new RenderTexture(width, height, depth, RenderTextureFormat.ARGB32);
            r.Create();
            var camera = GetComponent<Camera>();
            camera.targetTexture = r;
            camera.Render();
            camera.targetTexture = null;

            m_RenderTextures.Add(r);
        }
        GUILayout.Space(20);

        if (GUILayout.Button("Clear RenderTexture Memory"))
        {
            foreach (var r in m_RenderTextures)
            {
                r.Release();
            }
            m_RenderTextures.Clear();
            GC.Collect();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }


        var rc = GUILayoutUtility.GetRect(new GUIContent(" "), GUIStyle.none);
        var rcW = 300;
        var rcH = 150;
        foreach (var r in m_Textures)
            GUI.DrawTexture(new Rect(rc.x, rc.y, rcW * 0.5f, rcH), r);
        foreach (var r in m_RenderTextures)
            GUI.DrawTexture(new Rect(rc.x + rcW * 0.5f, rc.y, rcW * 0.5f, rcH), r);
    }

    void DoManagedGUI()
    {
        int size = 0;
        foreach (var m in m_ManagedMemory)
        {
            size += m.Length;
        }
        GUILayout.Label($"Managed Memory allocated {size / 1000000} MB");
        if (GUILayout.Button($"Allocate {kAllocationUnityInMb}MB of Managed Memory"))
        {
            m_ManagedMemory.Add(new byte[kAllocationUnityInMb * kOneMB]);
        }

        GUILayout.Space(20);

        if (GUILayout.Button($"Dirty Managed Memory"))
        {
            foreach (var m in m_ManagedMemory)
            {
                for (int i = 0; i < m.Length; i++)
                {
                    m[i] = 0;
                }
            }
        }
        GUILayout.Space(20);

        if (GUILayout.Button("Clear Managed Memory"))
        {
            m_ManagedMemory.Clear();
            GC.Collect();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    internal static string UInt64ToSizeString(UInt64 value)
    {
        if (value < 0)
            return "unknown";
        if (value == 0)
            return "0 Bytes";
        float val = (float)value;
        string[] scale = new string[] { "TB", "GB", "MB", "KB", "Bytes" };
        int idx = scale.Length - 1;
        while (val > 1000.0f && idx >= 0)
        {
            val /= 1000f;
            idx--;
        }

        if (idx < 0)
            return "<error>";

        return string.Format("{0:#.##} {1}", val, scale[idx]);
    }

    void OnGUI()
    {
#if !UNITY_EDITOR
        GUI.matrix = Matrix4x4.Scale(Vector3.one * 5);
#endif
        GUILayout.Label($"Profiler Total Allocated: {UInt64ToSizeString((ulong)Profiler.GetTotalAllocatedMemoryLong())}");
        GUILayout.Label($"Profiler Total Reserved: {UInt64ToSizeString((ulong)Profiler.GetTotalReservedMemoryLong())}");
        GUILayout.Label($"System Memory Size: {SystemInfo.systemMemorySize} MB");
        GUILayout.Space(20);
        m_MemoryType = (MemoryType)GUILayout.Toolbar((int)m_MemoryType, m_ToolbarItems);
        switch (m_MemoryType)
        {
            case MemoryType.Native: DoNativeGUI(); break;
            case MemoryType.Java: DoJavaGUI(); break;
            case MemoryType.Graphics: DoGraphicsGUI(); break;
            case MemoryType.Managed: DoManagedGUI(); break;
        }
    }
}
