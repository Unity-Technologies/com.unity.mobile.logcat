# View the device screen live

This page explains how to use the [Screen Capture tool](screen-capture.md) to view the screen of the connected device as it changes, and how to control the device from the Unity Editor.

The live view mirrors the device's display into the Unity Editor. Unlike a screenshot or a video, there is nothing to save: the view is live and stops when you select something else.

## View the screen

1. Open the [Device Screen Capture window](screen-capture-window-reference.md).
2. In the [Toolbar](screen-capture-window-reference.md#toolbar), use **Device Selector** to specify the device to view.
3. In the [Capture list](screen-capture-window-reference.md#capture-list), select the **Live** row. The Screen Capture tool starts streaming and displays the device's screen in the [Capture preview](screen-capture-window-reference.md#capture-preview).

The stream stops when you select another row in the list, close the window, or disconnect the device. Selecting a different device restarts the stream against the newly selected one.

The device only sends a frame when its screen changes, so **Frame rate** in the [Live view details](screen-capture-window-reference.md#live-view-details) drops to almost nothing while the device shows a still screen. This is expected: the last frame stays on display.

If the stream stops on its own, for example because the device restarted, right-click the **Live** row and select **Reconnect**.

## Control the device

While the live view is streaming, the Screen Capture tool sends your input to the device. **Input** in the [Live view details](screen-capture-window-reference.md#live-view-details) shows whether the device accepts it.

| **Input**                                  | **Result on the device**                                     |
| ------------------------------------------ | ------------------------------------------------------------ |
| Click or drag the image                    | A tap or a swipe at the same place on the device's screen.    |
| Scroll the wheel over the image            | Scrolls whatever is under the pointer.                        |
| Click the image, then type                 | Sends the keys you type, including Backspace, Enter, Tab and the arrow keys. |
| Escape                                     | Sends the Back key.                                           |
| Ctrl+A, Ctrl+C, Ctrl+V (Cmd on macOS)      | Select all, copy and paste on the device, using the device's own clipboard. |
| The **◄**, **●** and **■** buttons          | Sends the Back, Home and Overview keys. Useful on a device that uses gesture navigation, where the mirrored image has no navigation bar to tap. |

Other Ctrl and Cmd combinations are left to the Unity Editor, so its own shortcuts keep working while the image has focus.

> [!NOTE]
> Nothing is exchanged between the device's clipboard and your computer's. Ctrl+C copies on the device, and Ctrl+V pastes what was copied there.

## Change the size, quality and frame rate

Streaming a display uses both the device's CPU, to compress each frame, and the connection to your computer, to carry it. To trade quality for either, go to **Edit** > **Preferences** > **Analysis** > **Android Logcat Settings** (Windows) or **Unity** > **Settings** > **Analysis** > **Android Logcat Settings** (macOS) and use the [Live Stream](android-logcat-settings.md#live-stream) settings.

The settings apply when a stream starts. To apply them to a stream that is already running, right-click the **Live** row and select **Reconnect**.

> [!NOTE]
> If you connected the device with `adb connect` rather than by USB, the stream shares the device's Wi-Fi connection with everything else adb does, including the message log. Lower **Max Size** and **Max Frame Rate**, or connect the device by USB, if the connection struggles.

## Additional resources

* [Device Screen Capture window reference](screen-capture-window-reference.md)
* [Capture a screenshot](screen-capture-screenshot.md)
* [Capture a video](screen-capture-video.md)
* [Connect to a device](connect-to-a-device.md)
