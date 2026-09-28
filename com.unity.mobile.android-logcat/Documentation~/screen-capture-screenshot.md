# Capture a screenshot

This page explains how to use the [Screen Capture tool](screen-capture.md) to capture a screenshot from the connected device and save it as a file on your computer.

1. Open the [Device Screen Capture window](screen-capture-window-reference.md).
2. In the [Toolbar](screen-capture-window-reference.md#toolbar), use **Device Selector** to specify to device to take a screenshot of.
3. Set **Screen Capture Mode** to **Screenshot**.
4. Select the **Live** row in the [Capture list](screen-capture-window-reference.md#capture-list), then select **Take Screenshot** in the [Live view details](screen-capture-window-reference.md#live-view-details). Pressing Ctrl+Shift+S (Cmd+Shift+S on macOS) does the same from anywhere in this window. The Screen Capture tool takes a screenshot of the connected device, displays it in the [Capture preview](screen-capture-window-reference.md#capture-preview), and adds it to the list.
5. Right-click the screenshot's row, select **Copy To...**, and use the file explorer to save a copy of the image file elsewhere on your computer. Select several screenshots first to copy them all into one folder.

Every screenshot you capture is kept, so you do not have to save one before taking the next. Screenshots are named `<device id>_<number>.png` and are stored in your project, in `Library/AndroidLogcat/Screenshots`, unless you point [Captures Folder](android-logcat-settings.md#capture-settings) somewhere else. The default folder is local to your machine and is not part of your build, so use **Copy To...**, or a captures folder of your own, to keep a screenshot permanently.

Each screenshot is saved with a `.json` file of the same name beside it, recording the device it came from - its name, id, Android version, API level, ABI and display size - and when it was captured. The [Screenshot details](screen-capture-window-reference.md#screenshot-details) beside the image read it, and it is renamed, deleted and saved along with the image, so a copy you keep elsewhere still knows where it came from. A screenshot without one still opens; its device details read `Undefined`.

To rename or delete a screenshot, or to show it in Explorer or Finder, use the [Capture list](screen-capture-window-reference.md#capture-list). To look at part of a screenshot more closely, [zoom into it](screen-capture-window-reference.md#zoom-into-the-image).

## Additional resources

* [Device Screen Capture window reference](screen-capture-window-reference.md)
* [Capture a video](screen-capture-video.md)
* [View the device screen live](screen-capture-live-stream.md)