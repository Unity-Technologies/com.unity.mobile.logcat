# Memory window

The Memory window tracks the memory allocated for your application.

To open the Memory window in the Unity Editor:

1. Open the [Android Logcat window](android-logcat-window.md).
2. In the [Toolbar](android-logcat-window.md#toolbar), select **Tools** > **Memory Window** then either **Auto Capture** or **Manual Capture**.

If you select **Auto Capture**, Unity periodically captures memory snapshots for the selected application. If you select **Manual Capture**, the memory window provides a **Capture** button which you can use to manually capture a memory snapshot. This is useful if the automatic memory requests affect the application's performance.

> [!NOTE]
> When the Memory window requests a memory capture, the following message might appear in the Android Logcat window: `Explicit concurrent copying GC freed 5515(208KB) AllocSpace objects, 1(20KB) LOS objects, 49% free, 1926KB/3852KB, paused 46us total 11.791ms`. This is normal and it stops appearing when you disable the Memory window stops this message from appearing.

This section of the documentation describes the areas and features of the Memory window.

| **Topic**                                             | **Description**                         |
| ----------------------------------------------------- | --------------------------------------- |
| [Memory window reference](memory-window-reference.md) | Understand the Memory window interface. |