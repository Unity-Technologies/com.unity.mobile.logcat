# Capture a screenshot

This page explains how to use the [Screen Capture tool](screen-capture.md) to capture a screenshot from the connected device and save it as a file on your computer.

1. Open the [Device Screen Capture window](screen-capture-window-reference.md).
2. In the [Toolbar](screen-capture-window-reference.md#toolbar), use **Device Selector** to specify to device to take a screenshot of.
3. Set **Screen Capture Mode** to **Screenshot**.
4. Select **Capture**, or press Ctrl+Shift+S (Cmd+Shift+S on macOS). The Screen Capture tool takes a screenshot of the connected device, displays it in the [Capture preview](screen-capture-window-reference.md#capture-preview), and adds it to the [Capture list](screen-capture-window-reference.md#capture-list).
5. Select **Save As** and use the file explorer to save a copy of the image file elsewhere on your computer.

Every screenshot you capture is kept, so you do not have to save one before taking the next. Screenshots are stored in your project, in `Library/AndroidLogcat/Screenshots`, and named `<device id>_<number>.png`. That folder is local to your machine and is not part of your build, so use **Save As** to keep a screenshot somewhere permanent.

Each screenshot is saved with a `.json` file of the same name beside it, recording the device it came from - its name, id, Android version, API level, ABI and display size - and when it was captured. The [Screenshot details](screen-capture-window-reference.md#screenshot-details) beside the image read it, and it is renamed, deleted and saved along with the image, so a copy you keep elsewhere still knows where it came from. A screenshot without one still opens; its device details read `Undefined`.

To rename or delete a screenshot, or to show it in Explorer or Finder, use the [Capture list](screen-capture-window-reference.md#capture-list). To look at part of a screenshot more closely, [zoom into it](screen-capture-window-reference.md#zoom-into-the-image).

## Additional resources

* [Device Screen Capture window reference](screen-capture-window-reference.md)
* [Capture a video](screen-capture-video.md)
* [View the device screen live](screen-capture-live-stream.md)