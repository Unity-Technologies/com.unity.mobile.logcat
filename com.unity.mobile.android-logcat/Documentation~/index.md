## **Overview**

Android Logcat Package is a utility for displaying log messages coming from an Android device in the Unity Editor. Read more about [Android Logcat Document](https://developer.android.com/studio/command-line/logcat).

#### Requirements
- Compatible with Unity 2019.2 or above.
- Requires Unity's Android support module.

####  Supported features
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
        - Process Id
		- Text
- Auto run
- Screen capture
- Memory Window
- Stacktrace resolving

## **Using Android Logcat**

The toolbar is on the top of the window. Most Android Logcat controls can be found here.  
![Toolbar](images/android_logcat_toolbar.png)

#### Auto Run
When **Auto Run** is toggled, Android Logcat window will be launched automatically if you do **Build And Run** in **Build Settings** window.

#### Known Issues
##### **Don't run more than one instance of Editor from different locations**
  Android Logcat package uses adb process from Android SDK which is installed together with Unity. Unity periodically kills adb instances if they are from different Android SDK location than the one set in Preferences->External Tools->Android SDK Tools. This may interfere with Android Logcat package work.
