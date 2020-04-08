package com.unity3d.player;

import java.util.ArrayList;
import java.util.List;

public class JavaMemory
{
    static List<byte[]> allocations = new ArrayList<byte[]>();

    public static int allocateMemory(int bytes)
    {
        allocations.add(new byte[bytes]);
        return getTotalMemoryMB();
    }

    public static void deallocateMemory()
    {
        if (allocations.size() == 0)
            return;
        allocations.remove(0);
        System.gc();
    }

    public static  void dirtyMemory()
    {
        for (byte[] data: allocations)
        {
            for (int i = 0; i < data.length; i++)
                data[i] = (byte)( i % 255);
        }
    }

    public static void clearMemory()
    {
        allocations.clear();
        System.gc();
    }

    public static int getTotalMemoryMB()
    {
        int total = 0;
        for (byte[] data: allocations)
        {
            total += data.length;
        }
        return total / 1000000;
    }
}
