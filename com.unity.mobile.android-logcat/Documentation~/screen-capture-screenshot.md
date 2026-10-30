# Capture a screenshot

This page explains how to use the [Screen Capture tool](screen-capture.md) to capture a screenshot from the connected device and save it as a file on your computer.

1. Open the [Device Screen Capture window](screen-capture-window-reference.md).
2. In the [Toolbar](screen-capture-window-reference.md#toolbar), use **Device Selector** to specify the device to take a screenshot of.
3. In the [Capture list](screen-capture-window-reference.md#capture-list), select **Live**, then select **Take Screenshot** in the [Live view details](screen-capture-window-reference.md#live-view-details). Alternatively, press **Ctrl+Shift+S** (macOS: **Cmd+Shift+S**) while the live view displays. The Screen Capture tool takes a screenshot of the connected device, displays it in the [Capture preview](screen-capture-window-reference.md#capture-preview), and adds it to the list.
4. To save a copy of the screenshot elsewhere on your computer, right-click its row in the Capture list, select **Copy To...**, and use the file explorer to choose the location. To copy several screenshots into one folder, select them first and then right-click.

Every screenshot you capture is kept, so you don't have to save one before taking the next. Screenshots are named `<device id>_<number>.png` and are stored in your project, in `AndroidLogcat/Captures`, unless you set [Captures Folder](android-logcat-settings.md#capture-settings) to another folder. The default folder is outside `Assets` and isn't part of your build, so use **Copy To...**, or a captures folder of your own, for a screenshot you want to share.

Each screenshot is saved with a `.json` file of the same name beside it. The `.json` file records the device the screenshot came from and when it was captured. The device information includes the device name, ID, Android version, API level, ABI, and display size. The [Capture details](screen-capture-window-reference.md#capture-details) beside the image display this information. When you rename, delete, or copy a screenshot, the Screen Capture tool renames, deletes, or copies its `.json` file too, so a copy you save elsewhere still records the device it came from. A screenshot without a `.json` file still opens, and its device details display `Undefined`.

To rename or delete a screenshot, or to show it in Explorer or Finder, use the [Capture list](screen-capture-window-reference.md#capture-list). To look at part of a screenshot more closely, [zoom into it](screen-capture-window-reference.md#zoom-into-the-image).

## Additional resources

* [Device Screen Capture window reference](screen-capture-window-reference.md)
* [Capture a video](screen-capture-video.md)
* [View the device screen live](screen-capture-live-stream.md)
