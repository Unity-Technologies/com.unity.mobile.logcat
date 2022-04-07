# Capture a video

This page explains how to use the [Screen Capture tool](screen-capture.md) to capture a video of the connected device's screen and save it as a file on your computer.

1. Open the [Device Screen Capture window](screen-capture-window-reference.md).
2. In the [Toolbar](screen-capture-window-reference.md#toolbar), use **Device Selector** to specify to device to take a screenshot of.
3. Set **Screen Capture Mode** to **Video**.
4. Select **Capture**. The Screen Capture tool begins to capture a video of the connected device's screen.
5. When you want to finish the video, select **Stop**. The Screen Capture tool finishes capturing the video and displays it in the [Capture preview](screen-capture-window-reference.md#capture-preview).
6. Select **Save As** and use the file explorer to save the video file to your computer.

## Details

* When recording a video, the Screen Recorder Tool doesn't capture sound.
* The Screen Recorder Tool uses Unity's video player for the video preview. If your operating system is Windows, you might see `WindowsVideoMedia` warnings in the Editor console. This is because the video is not processed.
* If the entire video contains a static image, the video only contains one frame and the length of the video will be zero.
* On Chrome OS, you can only capture the video from the sandboxed app which is the one you install manually via adb or Unity. You can't capture video from the desktop or from built-in apps.

## Additional resources

* [Device Screen Capture window reference](screen-capture-window-reference.md)
* [Capture a screenshot](screen-capture-screenshot.md)