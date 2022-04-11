# Messages

There can be multiple applications on a connected Android device. The [Android Logcat window](android-logcat-window.md) can display messages from:

* Every application running on the device.
* A specific application running on the device.

By default, the Android Logcat window displays message from every application on the [connected Android device](connect-to-a-device.md). This page explains how to configure the Android Logcat window to only display message from a specific application.

If you want to view messages for a specific application, you can either:

* Select an application running on the Android device.
* Use **Auto Run** to automatically connect to and view messages for applications that you build and run.

## Select an application

To select an application to view messages for:

1. [Connect and select](connect-to-a-device.md) the Android device that is running the application.
2. Open the Android Logcat window.
3. From the [toolbar](android-logcat-window-reference.md#toolbar), select the **Package Selector**. The **Package Selector** contains:
    * The application for the top [activity](https://developer.android.com/guide/components/activities/intro-activities) currently running on the selected device.
    * The application built from your Unity Project, if its running on the selected device. It doesn't need to be the top running activity.
4. In the drop-down menu, select the application to connect to. After you do this, the Android Logcat window displays messages for the selected application.

> [!IMPORTANT]
> Android Logcat uses the [applicationIdentifier](https://docs.unity3d.com/ScriptReference/PlayerSettings-applicationIdentifier.html) Player Setting to identify Unity applications. If you build a Unity application with one `applicationIdentifier` and change it in your Unity Project, Android Logcat can't identify the application on the Android Device if the application is running in the background.

## Use auto run

If you want to connect the Android Logcat window to your application whenever you build and run the application, enable **Auto Run**. To do this:

1. Open the Android Logcat window.
2. From the [toolbar](android-logcat-window-reference.md#toolbar), select **Auto Run**.
3. Open [Android Build settings](https://docs.unity3d.com/2021.2/Documentation/Manual/android-build-settings.html#), and [Build and run your application](https://docs.unity3d.com/Manual/android-BuildProcess.html). When the application starts on the Android device, the Android Logcat window automatically connects to the application and displays the message log for it.

## Additional resources

* [Android Logcat window](android-logcat-window.md)