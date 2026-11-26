# Memory window reference

This page introduces the [Memory window's](memory-window-reference.md) interface. The interface for the Memory window is part of the [Android Logcat window](android-logcat-window.md).

To open the Memory window in the Unity Editor:

1. Open the [Android Logcat window](android-logcat-window.md).
2. In the [Toolbar](android-logcat-window-reference.md#toolbar), select **Tools** > **Window** > **Memory**.

If you select **Auto Capture**, Unity periodically captures memory snapshots for the selected application. If you deselect **Auto Capture**, the memory window provides a **Capture** button which you can use to manually capture a memory snapshot. This is useful if the automatic memory requests affect the application's performance.

![The memory window](images/memory-window.png)
> The memory window.

| **Label**               | **Description**                                              |
| ----------------------- | ------------------------------------------------------------ |
| ![Label A](images/label-a.png) | [Memory details panel](#memory-details-panel): Displays the amount of allocated memory for each memory type. |
| ![Label B](images/label-b.png) | [Memory chart](#memory-chart): Contains a chart of the memory allocated for the connected application over time. |

## Memory details panel

The memory details panel displays the amount of memory for each memory type. It can display different memory groups and.

![The memory details panel](images/memory-window-details-panel.png)
> The memory details panel.

| **Property**         | **Description**                                              |
| -------------------- | ------------------------------------------------------------ |
| **Group**            | Specifies the memory group to display memory allocation for. For more information, see [Memory groups](#memory-groups). |
| **Allocated Memory** | Lists types of memory and the amount of memory allocated to each type. |
| **Capture**          | Manually captures a snapshot of the memory allocated to your application. For information on how the Memory window captures the snapshot, see [Memory requests](#memory-requests). |

### Memory groups

The Memory window can display different memory groups allocated for your application.

![The memory group drop-down](images/memory-groups.png)
> The memory group drop-down.

| **Memory group**                | **Description**                                              |
| ------------------------------- | ------------------------------------------------------------ |
| **Resident Set Size (RSS)**     | The total amount of memory in RAM that the application allocated. This includes both shared and non-shared memory pages. For example, applications that access the same library share memory pages.<br/> **Note**: This metric is only visible on Android 11 or higher. |
| **Proportional Set Size (PSS)** | The total amount of memory in RAM that the application actively uses. This is not the total memory that the application allocates. For example, if the application allocates memory from the native heap but doesn't read from or write to the memory, the memory doesn't appear in PSS memory.<br/> Note: If several processes share a memory page, the size contribution of the page to PSS memory is proportional to the amount of memory and number of processes that share it. For example, if two processes share 20MB of graphics memory, the application's PSS memory only shows 10MB. |
| **Heap Alloc**                  | The total amount of memory the application allocates using Dalvik (Java allocators) and native heap allocators. This includes both memory which is in RAM or is paged in storage. This is the best metric when checking if the application is leaking Native or Java memory. |
| **Heap Size**                   | The total memory that the application reserves. This memory size will be always bigger than **Heap Alloc**. |
| **Anonymous RSS And Swap**      | The application's memory that isn't backed by a file on storage, such as Java and native heap allocations and anonymous memory mappings. **Anonymous RSS** is the part held in RAM, and **Swap** is the part the system compressed into zRAM. This is the **Memory usage (Anonymous RSS + Swap)** metric that Google Play reports in [Android vitals](https://developer.android.com/topic/performance/vitals/memory-usage) and checks against the memory thresholds in the [Play Console technical quality requirements](https://support.google.com/googleplay/android-developer/answer/17492799). Play evaluates the 90th percentile of the values collected from users' devices, so a single measurement only gives an indication.<br/> **Note**: The values come from `RssAnon` and `VmSwap` in `/proc/<pid>/status`, which require Linux kernel 4.5 or later. On older devices this group is empty. |

### Memory types

Depending on how you allocate memory in your application, Android creates memory of different types.

* If you allocate memory using the native functions **malloc** and **new**, or use C# **Marshal.AllocHGlobal**, this memory appears in the **Heap Alloc** and **Heap Size** groups under the **Native Heap** memory type.
* If you allocate memory using java functions like **new**, this memory appears in the **Heap Alloc** and **Heap Size** groups under the **Java Heap** memory type.
* In both cases above, both native and java memory won't appear in the **PSS** memory group until you try to write to or read from the allocated native or java memory.
* If you allocate memory using the C# **new** function, this memory appears in the **PSS** group under the **Private Other** memory type.

For more information, see [meminfo](https://developer.android.com/studio/command-line/dumpsys#meminfo).

To toggle which memory types appear in the [memory chart](#memory-chart), click the memory type in the [memory details panel](#memory-details-panel).

### Memory requests

To make memory requests, the Memory window uses `adb shell dumpsys meminfo package_name`. For more information, see [dumpsys](https://developer.android.com/studio/command-line/dumpsys#meminfo). For the **Anonymous RSS And Swap** group, it also reads `adb shell cat /proc/<pid>/status`.

While the **Anonymous RSS And Swap** group is selected, the Memory window only reads `/proc/<pid>/status` and doesn't run `dumpsys meminfo`. `dumpsys meminfo` triggers a garbage collection in the application, which lowers the Java heap and affects the measurement. The charts of the other memory groups don't get new snapshots during that time. `/proc/<pid>/status` is read with every snapshot, so the **Anonymous RSS And Swap** chart keeps updating while other groups are selected.

The following screenshot shows the memory dump that `dumpsys` produces:

![A raw dumpsys memory dump](images/memory-dump.png)
> A raw `dumpsys` memory dump.

## Memory chart

The memory chart displays the memory allocated for the connected application over time.

 ![The memory chart](images/memory-window-chart.png)
> The memory chart.

To view a snapshot in the [memory details panel](#memory-details-panel), click on the chart at the part you want to view.
