using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class MemoryAllocations : MonoBehaviour
{
    const int kAllocationUnit = 10 * 1000 * 1000;
    const int kAllocationUnityInMb = kAllocationUnit / 1000000;
    struct NativeMemoryData
    {
        internal IntPtr pointer;
        internal int size;
    }

    List<NativeMemoryData> m_NativeAllocations = new List<NativeMemoryData>();

    AndroidJavaObject m_JavaClass;
    // Start is called before the first frame update
    void Start()
    {
        m_JavaClass = new AndroidJavaObject("com.unity3d.player.JavaMemory");
        if (m_JavaClass == null)
            throw new Exception("Failed to find com.unity3d.player.JavaMemory");
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

    private void ClearJavaMemory()
    {
        m_JavaClass.CallStatic("clearMemory");
    }

    private int GetAllocatedJavaMemoryMB()
    {
        return m_JavaClass.CallStatic<int>("getTotalMemoryMB");
    }

    void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(Vector3.one * 5);
        GUILayout.Label($"Native Memory allocated {GetAllocatedNativeMemoryMB()} MB");
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

        if (GUILayout.Button("Clear Native Memory"))
        {
            ClearNativeMemory();
        }

        GUILayout.Space(20);
        GUILayout.Label($"Java Memory allocated {GetAllocatedJavaMemoryMB()} MB");
        if (GUILayout.Button($"Allocate {kAllocationUnityInMb}MB of Java Memory"))
            AllocateJavaMemory(kAllocationUnit);

        GUILayout.Space(20);
        if (GUILayout.Button($"Deallocate {kAllocationUnityInMb}MB of Java Memory"))
        {
            DeallocateJavaMemory();
        }
        GUILayout.Space(20);

        if (GUILayout.Button("Clear Java Memory"))
        {
            ClearJavaMemory();
        }
    }
}
