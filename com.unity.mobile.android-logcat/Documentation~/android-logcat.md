# **_Android Logcat Guide_**

## **Overview**

Android Logcat Package is a utility for displaying log messages coming from Android device in Unity Editor. Read more about [Android Logcat Document](https://developer.android.com/studio/command-line/logcat).

**Requirements**
- Compatible with Unity 2019.1 or above.
- Requires Unity's Android support module.
- Active platform in Unity has to be set to Android.

**Supported features**
- Device connnection
	- Via USB
	- Via Wifi
- Device selection
- Package selection
- Log 
	- Copy
	- Save
	- Clear
	- Filter by
		- Priority
		- Tag
	- Search by
		- Text
		- Regex
- Auto run
- Screen capture
- Stacktrace resolving

## **Installing Android Logcat** 
To install Android Logcat package, follow the instructions in the [Package Manager documentation](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@latest/index.html). 

You can access Android Logcat window in Unity Editor via
- Menu: **Window \> Analysis \> Android Logcat**
- Shortcut: **Alt+6** on Windows, **Option+6** on macOS.

![Android Logcat Window](images/android_logcat_window.png)

## **Using Android Logcat**

The toolbar is on the top of the window. Most Android Logcat controls can be found here.  
![Toolbar](images/android_logcat_toolbar.png)

### Device Connection
Connecting devices through USB and Wifi are both supported.

**Connect via USB**  
The devices connected through USB will be added to the device list automatically when starting Android logcat window. 

**Connect via Wifi**  
Go to the devices list and click **\<Enter IP>** as below.  
![Connect via Wifi](images/connect_via_wifi.png)

And a window to enter an ip address will be popped up as below.  
![Enter IP](images/enter_ip_window.png)

**Device Selection**  
The device list contains all the connnected devices, but you can only select one device at a time from the list.

### Package Selection
The package selection list contains:
- Package of the top activity which is currently running on the selected device.
- Package from the player settings if it's running on the selected device (Doesn't have to be the the top running activity).

You can only select one package at a time, only the log messages come from the selected package will be shown in the logcat console window. Alternatively you can select **No Filter**, messages coming from all package will be shown.

### Log Controls

The log window contains multiple predefined columns, you can enable/disable them by right clicking on the columns.  
![Log Columns](images/log_columns.png)

**Copy**  
The selected logs can be copied to the clipboard. You can right click on the messages and select **Copy**.

**Save**  
The selected logs can be saved to file. You can right click on the messages and select **Save Selection**.

**Clear**  
You can clear all the logs by click the **Clear** button on the toolbar.

**Search**  
You can input text in the text field on the toolbar to search logs by text. You can also toggle **Regex** on to treat contents in the text field as regex expression.

#### Filter
- By Priority  
  You can right click on the **Priority** column to select or deselect priorities. Priorities are predefined according to the [Android Logcat Document](https://developer.android.com/studio/command-line/logcat#filteringOutput).

- By Tag  
  Right click on the **Tag** column to select or deselect tags. You can also add or remove a tag by following steps:
  	1. Find a message with the tag you want to add.
	2. Right click on the message and select **Add Tag** or **Remove Tag**.  
	![Tag Control](images/tag_control.png)

### Screen Capture
You can use **Capture Screen** button on the toolbar to capture a screenshot of the selected device. You can also **Save** the screenshot as file.  
![Device Screen Capture](images/device_screen_capture.png)

### Auto Run
When **Auto Run** is toggled, Android Logcat window will be launched automatically if you do **Build And Run** in **Build Settings** window.

### Stacktrace Resolving
One benefit of using Android Logcat package is automatic stacktrace resolving. The **addr2line** tool in Android NDK is used to convert the addresses in the crash logs to the file names and line numbers. Below is an example of what's added to log.

**_The original log from Android logcat_**

	E CRASH   :      #01  pc 01c65330  /data/app/com.CrashComp.Crash-J2Z_L0XSsSAZPkt9lab2rQ==/lib/arm/libunity.so(DiagnosticsUtils_Bindings::ForceCrash(DiagnosticsUtils_Bindings::ForcedCrashCategory, ScriptingExceptionPtr*)+48)

**_The log shown in Android Logcat window in Unity Editor_**

	Error CRASH: 	#01  pc 01c65330  /data/app/com.CrashComp.Crash-J2Z_L0XSsSAZPkt9lab2rQ==/lib/arm/libunity.so DiagnosticsUtils_Bindings::ForceCrash(DiagnosticsUtils_Bindings::ForcedCrashCategory, ScriptingExceptionPtr*) at ../Runtime/Export/Diagnostics/DiagnosticsUtils.bindings.cpp:25

To use this feature, you need to 
- Have Android NDK installed and set the NDK path in Unity (Menu: **Editor** \> **Preferences...** \> **External Tools**).
- Have the corresponding symbol files installed with Unity.

## **Document Revision History**
|Date|Reason|
|---|---|
|Jan 22, 2019|Document Updated. Matches package version 0.2.1.|
|Nov 16, 2018|Document Updated. Matches package version 0.1.3.|
|Oct 30, 2018|Document Updated. Matches package version 0.1.2.|
|Oct 23, 2018|Document created. Matches package version 0.1.1.|