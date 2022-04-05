# Android Logcat window reference

This page introduces the different parts of the Android Logcat window's interface.

![](images/android-logcat-window.png)
> The Anrdoid Logcat window.

| **Label**               | **Description**                                              |
| ----------------------- | ------------------------------------------------------------ |
| ![](images/label-a.png) | [Toolbar](#toolbar): Contains options and tools for the Android Logcat window. |
| ![](images/label-b.png) | [Message log](#message-log): Lists the messages that Unity receives from Android Logcat. |

## Toolbar

The toolbar contains options that customize the message log section and also additional tools that relate to Android Logcat and the connected Android device.

![](images/android-logcat-window-toolbar.png)
> The Android Logcat window toolbar.

| **Toolbar option**   | **Description**                                              |
| -------------------- | ------------------------------------------------------------ |
| **Auto Run**         | Toggles auto-run. When enabled, the Android Logcat window launches automatically when you [Build And Run](https://docs.unity3d.com/2022.2/Documentation/Manual/android-BuildProcess.html) your application. For more information, see [Use auto run](view-messages.md#use-auto-run). |
| **Device Selector**  | Specifies the Android device to connect the Android Logcat window to. For more information, see [Connect to a device](connect-to-a-device.md). |
| **Package Selector** | Specifies the application on the Android device to display messages for. For more information, see [Select an application](view-messages.md#select-an-application). |
| **Filter Input**     | A search field that you can use to filter the [message log](#message-log) by text. For more information, see [Filter the message logs](android-logcat-window-message-log-filter). |
| **Filter Options**   | Options that determine how to use **Filter Input** to filter messages in the message log. The options are:<br/>&#8226; **Use Regular Expressions**: Indicates whether to treat the **Filter Input** as a regular expression. <br/>&#8226; **Match Case**: Indicates whether to make the filter case-senstive or not. <br/><br/>For more information, see [Filter the message logs](android-logcat-window-message-log-filter). |
| **Reconnect**        | Reconnects the Android Logcat window to the                  |
| **Disconnect**       | Disconnects the Android Logcat window from the               |
| **Clear**            | Clears the list of messages in the [message log](#message-log). |
| **Tools**            | A drop-down list of tools that can help you to debug your Android application. The options are:**Screen Capture**: Captures screenshots and videos of the connected Android device. For more information, see [Screen capture tool](screen-capture.md).**Open Terminal**: Opens the terminal at **Stacktrace Utility**:**Memory Window**: |

## Message log

The message log section displays the messages that Unity receives from Android Logcat. It displays information for each message in predefined columns.

![](images/android-logcat-window-message-log.png)
> Message log column names.

| **Column name** | **Description**                                              |
| --------------- | ------------------------------------------------------------ |
| **Time**        | The time that the message was produced.                      |
| **Pid**         | The ID of the process that produced the message.             |
| **Tid**         | The ID of the thread that produced the message.              |
| **Priority**    | The message's priority. For more information about message priority, see [Filtering log output](https://developer.android.com/studio/command-line/logcat#filteringOutput). |
| **Tag**         | The tag associated with the message.                         |
| **Message**     | The message text.                                            |

### Message log controls

The message log contains functionality that helps you to navigate through and share messages.

| **Control**  | **Description**                                              |
| ------------ | ------------------------------------------------------------ |
| **Copy**     | To copy the selected logs to the clipboard, right-click the selected messages and select **Copy**. |
| **Save**     | To save the selected logs to a file on your computer, right-click the selected messages and select **Save**. |
| **Navigate** | To navigate through message logs using the keyboard, use the arrow keys. |

## Additional resources

* [Customize message log columns](android-logcat-window-message-log-customize.md).
* [Filter the message log](android-logcat-window-message-log-filter.md).