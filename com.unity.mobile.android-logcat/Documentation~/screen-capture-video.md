# Capture a video

This page explains how to use the [Screen Capture tool](screen-capture.md) to record the connected device's screen.

1. Open the [Device Screen Capture window](screen-capture-window-reference.md).
2. In the [Toolbar](screen-capture-window-reference.md#toolbar), use **Device Selector** to specify the device to record.
3. Select **Live** in the [Capture list](screen-capture-window-reference.md#capture-list) to view the device's screen.
4. Select **Start Recording** in the [Live view details](screen-capture-window-reference.md#live-view-details). The Screen Capture tool begins to record the device's screen, and the button becomes **Stop Recording**.
5. Select **Stop Recording** to finish. The recording is added to the [Capture list](screen-capture-window-reference.md#capture-list), and its row is briefly highlighted. Select the row to open it in the [Capture preview](screen-capture-window-reference.md#capture-preview). The Capture list is disabled while recording. Closing the window also stops the recording.
6. To keep a copy elsewhere, right-click the recording's row and select **Copy To...**.

Recordings are stored beside screenshots, in the folder set by [Captures Folder](android-logcat-settings.md#capture-settings), and are named `<device id>_<number>.mp4`.

A recording is made from the live view, so it uses the same orientation as the live view. By default, the video is square, and its width and height match the longest dimension of the live stream. When you rotate the device, or unfold a foldable device, the display stays at full size. The rest of the square is black. To record at the size of the screen instead, disable **Square Video** in the [Recording settings](android-logcat-settings.md#recording).

To change the size, quality, and frame rate of a recording, use **Max Size**, **JPEG Quality**, and **Max Frame Rate** in the [Live Stream settings](android-logcat-settings.md#live-stream).

The device sends a frame only when its screen changes. While the screen is still, the recording repeats the last frame. A recording stops automatically after the **Time Limit** in the [Recording settings](android-logcat-settings.md#recording), which is 10 minutes by default. A recording also stops and is saved when Unity reloads scripts, for example after you change a script.

## Record logcat messages

A recording can also save the logcat messages the device logs while you record, so that you can see what the device logged at each frame when you play the recording back.

1. Select the arrow next to **Start Recording** in the [Live view details](screen-capture-window-reference.md#live-view-details).
2. Select **Include Logcat Messages**. This option is disabled by default, and applies from the next recording you start.

The recording saves the messages the [Android Logcat window](android-logcat-window.md) would display: the selected package, priority, tags, and filter text when recording starts. The messages are saved in the `.json` file next to the recording.

When you play the recording in the [Capture preview](screen-capture-window-reference.md#capture-preview), the messages appear in a panel under the video. Each message belongs to the first frame captured after it was logged, and starts with that frame's time in the video, as the player displays it, followed by the time on the device. The messages of the current frame, logged since the previous frame, are highlighted, and messages logged after the current frame are dimmed. Select a message to go to its frame. Drag the divider above the panel to resize it.

## Recording limitations

* When recording a video, the Screen Recorder Tool doesn't capture sound.
* The Unity Editor encodes the video when a recording runs, which uses Editor processing time. A larger stream increases this processing time.
* The Linux Editor can't record, or play recordings back, because it doesn't support H.264 video.

## Additional resources

* [Device Screen Capture window reference](screen-capture-window-reference.md)
* [Capture a screenshot](screen-capture-screenshot.md)
