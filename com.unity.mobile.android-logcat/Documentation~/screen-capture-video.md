# Capture a video

This page explains how to use the [Screen Capture tool](screen-capture.md) to record the connected device's screen.

1. Open the [Device Screen Capture window](screen-capture-window-reference.md).
2. In the [Toolbar](screen-capture-window-reference.md#toolbar), use **Device Selector** to specify the device to record.
3. Select **Live** in the [Capture list](screen-capture-window-reference.md#capture-list) to view the device's screen.
4. Select **Take Recording** in the [Live view details](screen-capture-window-reference.md#live-view-details). The Screen Capture tool begins to record the device's screen, and the button becomes **Stop Recording**.
5. Select **Stop Recording** to finish. The recording is added to the [Capture list](screen-capture-window-reference.md#capture-list), and its row is briefly highlighted. Select the row to open it in the [Capture preview](screen-capture-window-reference.md#capture-preview). The Capture list is disabled while recording. Closing the window also stops the recording.
6. To keep a copy elsewhere, right-click the recording's row and select **Copy To...**.

Recordings are stored beside screenshots, in the folder set by [Captures Folder](android-logcat-settings.md#capture-settings), and are named `<device id>_<number>.mp4`.

A recording is made from the live view, so it is the size of the stream and shows the view as it is turned. To record at a larger size or a higher quality, raise **Max Size** and **JPEG Quality** in the [Live Stream settings](android-logcat-settings.md#live-stream). Recordings play at 30 frames per second. The device only sends a frame when its screen changes, so while the screen is still, the recording repeats the last frame. There is no time limit.

## Details

* When recording a video, the Screen Recorder Tool doesn't capture sound.
* Recording uses the Editor to encode the video, which takes some of its time while a recording runs. A larger stream costs more.
* The Linux Editor can't record, or play recordings back, because it doesn't support H.264 video.
* On Chrome OS, you can only capture the video from the sandboxed app which is the one you install manually via adb or Unity. You can't capture video from the desktop or from built-in apps.

## Additional resources

* [Device Screen Capture window reference](screen-capture-window-reference.md)
* [Capture a screenshot](screen-capture-screenshot.md)
