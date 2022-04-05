# Memory window reference

The interface for the [Memory window](memory-window-reference.md) is part of the [Android Logcat window](android-logcat-window.md).

![](images/MemoryWindow.png)> The memory window.

| **Label**               | **Description**                                              |
| ----------------------- | ------------------------------------------------------------ |
| ![](images/label-a.png) | [Memory details panel](#memory-details-panel): Displays the amount of allocated memory for each memory type. |
| ![](images/label-b.png) | [Memory chart](#memory-chart): Contains a chart of the memory allocated for the connected application over time. |

## Memory details panel

The memory details panel displays the amount of memory for each memory type. It can display different memory groups and, if you open the Memory window in **Manual Capture** mode, trigger a manual memory snapshot capture.

![](images/memory-window-details-panel.png)
> The memory details panel.

| **Property**         | **Description**                                              |
| -------------------- | ------------------------------------------------------------ |
| **Group**            | Specifies the memory group to display memory allocation for. For more information, see [Memory groups](#memory-groups). |
| **Allocated Memory** | Lists types of memory and the amount of memory allocated to each type. |
| **Capture**          | Manually captures a snapshot of the memory allocated to your application. For information on how the Memory window captures the snapshot, see [Memory requests](#memory-requests). |

### Memory groups

The Memory window can display different memory groups allocated for your application.

| **Memory group**                | **Description**                                              |
| ------------------------------- | ------------------------------------------------------------ |
| **Resident Set Size (RSS)**     | The total amount of memory in RAM that the application allocated. This includes both shared and non-shared memory pages. For example, applications that access the same library share memory pages.<br/> **Note**: This metric is only visible on Android 11 or higher. |
| **Proportional Set Size (PSS)** | The total amount of memory in RAM that the application actively uses. This is not the total memory that the application allocates. For example, if the application allocates memory from the native heap but doesn't read from or write to the memory, the memory doesn't appear in PSS memory.<br/> Note: If several processes share a memory page, the size contribution of the page to PSS memory is proportional to the amount of memory and number of processes that share it. For example, if two processes share 20MB of graphics memory, the application's PSS memory only shows 10MB. |
| **Heap Alloc**                  | The total amount of memory the application allocates using Dalvik (Java allocators) and native heap allocators. This includes both memory which is in RAM or is paged in storage. This is the best metric when checking if the application is leaking Native or Java memory. |
| **Heap Size**                   | The total memory that the application reserves. This memory size will be always bigger than **Heap Alloc**. |

### Memory requests

To make memory requests, the Memory window uses `adb shell dumpsys meminfo package_name`. For more information, see [dumpsys](https://developer.android.com/studio/command-line/dumpsys#meminfo).

## Memory chart

The memory chart displays the memory allocated for the connected application over time.

 

> The memory chart.

To view a snapshot in the [memory details panel](#memory-details-panel), click on the chart at the part you want to view.

To toggle which memory types appear in the memory chart, click the memory type in the [memory details panel](#memory-details-panel).