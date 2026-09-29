# Capture a video

This page explains how to use the [Screen Capture tool](screen-capture.md) to record the connected device's screen.

1. Open the [Device Screen Capture window](screen-capture-window-reference.md).
2. In the [Toolbar](screen-capture-window-reference.md#toolbar), use **Device Selector** to specify the device to record.
3. Select **Live** in the [Capture list](screen-capture-window-reference.md#capture-list) to view the device's screen.
4. Select **Take Recording** in the [Live view details](screen-capture-window-reference.md#live-view-details). The Screen Capture tool begins to record the device's screen, and the button becomes **Stop Recording**.
5. Select **Stop Recording** to finish. The recording is added to the [Capture list](screen-capture-window-reference.md#capture-list) and selected, which plays it in the [Capture preview](screen-capture-window-reference.md#capture-preview). Selecting a capture from the list, or closing the window, also stops the recording.
6. To keep a copy elsewhere, right-click the recording's row and select **Copy To...**.

Recordings are stored beside screenshots, in the folder set by [Captures Folder](android-logcat-settings.md#capture-settings), and are named `<device id>_<number>.mp4`. To change how long a recording runs, its size or its bit rate, use the **Recording** settings in [Preferences](android-logcat-settings.md#capture-settings).

## Details

* When recording a video, the Screen Recorder Tool doesn't capture sound.
* The Screen Recorder Tool uses Unity's video player for the video preview. If your operating system is Windows, you might see `WindowsVideoMedia` warnings in the Editor console. This is because the video is not processed.
* If the entire video contains a static image, the video only contains one frame and the length of the video will be zero.
* On Chrome OS, you can only capture the video from the sandboxed app which is the one you install manually via adb or Unity. You can't capture video from the desktop or from built-in apps.

## Additional resources

* [Device Screen Capture window reference](screen-capture-window-reference.md)
* [Capture a screenshot](screen-capture-screenshot.md)
