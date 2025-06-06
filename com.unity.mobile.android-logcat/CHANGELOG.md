# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.4.6] - 2025-06-07
### Fixes & Improvements
 - Stacktrace window will validate buildId when resolving stacktrace, informing you if wrong symbol file is used. Reset symbol regexes if needed.

## [1.4.5] - 2025-04-11
### Fixes & Improvements
 - Fix "llvm-nm.exe: error: : unknown argument '-e'" when resolving stacktraces on Windows. Was happening with Unity version 6000.0.44f1
 
## [1.4.4] - 2024-12-07
### Fixes & Improvements
 - Default AutoScroll to Auto when there's not logcat settings saved
 - Fix a bug, where logcat would sometimes connect to wrong device when you have two or more devices connected to host. The fix is only available for Unity 6.1 or higher, since that's the version where the required application launch callback was introduced.

## [1.4.3] - 2024-09-09
### Fixes & Improvements
 - Fix Screen Capture window not saving last image/video save location correctly between Unity domain reloads or launches, previously it would always reset to Unity project directory.
 - Fix compatability issues with future Unity versions.
 
## [1.4.2] - 2024-05-13
### Fixes & Improvements
 - Fix issue, where logcat package would throw an error if there are multiple devices connected.

## [1.4.1] - 2024-04-12
### Fixes & Improvements
 - Logcat package now detects if the logs with a specific tag are disabled on a device (this was causing messages not to be displayed), and if so, displays a message with an option to fix such behavior.
 - Fixed an issue with MemoryWindow, where active package would get lost during domain reload.
 - Improved internal log to handle a large amount of messages.
 - Fixed an issue where logcat dispatcher would stop responding during domain reload.
 - Fixed an issue where MemoryWindow would fail to query memory stats using package name. It now uses process id instead.
 - Added Process Manager control which you can use to terminate an application or send a trim memory event.
 - Fixed tag filtering, previously logcat would check if incoming_tag contains tag_in_filter, now it will perform incoming_tag equals tag_in_filter. That way it's easier to filter messages with tags which have the same begining.
 
## [1.4.0] - 2023-11-21
### Fixes & Improvements
 - Fix stacktrace resolve regex for entry like '  #15  pc 0x0000000000a0de84  /data/app/com.DefaultCompany.NativeRuntimeException1-eStyrW-dxxC0QfRH6veLhA==/lib/arm64/libunity.so'. Reset regex to apply the fix
 - Added inputs window - Tools->Window->Inputs, where you can easily inject keyboard/text input on to android device.
 - Add Scroll option for messages which allows you to explicitly to always scroll to end without automatic logic.
 - Device selection dropdown will contain device name + id, if device name is not available, only id will be displayed.
 - You can now specify symbol file extensions in the new settings, Preferences->Android Logcat Settings> Symbol Extensions. Stacktrace Utility tool uses the symbol file extensions while looking for symbols to resolve stacktraces.
 
## [1.3.2] - 2022-04-12
### Fixes & Improvements
 - Fix integration tests not able to run on a specific build server configuration like Katana.
 - Fix stacktrace resolving, when Unity uses NDK 23.

## [1.3.1] - 2022-04-11
### Fixes & Improvements.
 - Docs updated.

## [1.3.0] - 2022-03-23
### Fixes & Improvements.
 - You can now perform text filtering when disconnected from the device, the filtering will be performed by the logcat package, previously it was performed by **adb logcat** command.
 - Text filtering has an option to ignore character casing.
 - Whenever entering new text filter selection of messages will persist, the selection will reset if **adb logcat** command is reexecuted, for ex., picking new tag, picking new priority, etc.
 - There are new settings in Preferences->Android Logcat Settings:
    - Max Cached Messages - controls how many unfiltered messages to keep in cache, you can remove the limit, and have as many messages as your memory let's you.
    - Max Displayed Messages - controls how many messages display on the list, you can remove the limit, but it might cause UI issues.
 - Fixed tiny issue, when right clicking on the log message, the tag and process id for context menu was being taken from the first selected item, now it will be taken from item which you're hovering on
 - Fixed issue, when right clicking on the log message with tag containing forward slash, the menu would be incorrectly displayed.
 - Fixed issue, where logcat package would freeze, if you would click Clear after disconnecting the device. 
 - Add setting for controlling how many exited packages to show in package selection.
 - Add doc about known issue where logcat might work incorrectly if there more than one Editor instance running.
 - You can now navigate through messages using arrow keys.
 - Bump minimum Unity version support from 2019.2 to 2019.4
 - Screen Capture has the ability to capture videos from the device, see the documentation for more details

## [1.2.2] - 2021-04-21

### Fixes & Improvements.
 - Fix error 'Unable to find required resource at Fonts/consola.ttf' when using logcat package with 2021.2.
 - Provide user friendly message when NDK directory is incorrect or is not set.
 - The NDK presence will only be checked when it's actually needed, for ex., when resolving stacktraces.
 - Don't show Add/Remove Tag menu item, if tag is empty.
 - Don't show Filter By Process Id menu item, if process id is invalid.
 - When using AutoRun, logcat will appear automatically if you're building only for Android, previously it would appear for any platform, which is undesired.

## [1.2.1] - 2021-01-21

### Fixes & Improvements.
 - Removed limitation where Android Logcat could only be used while the active Editor platform is Android. Note: Android Support is still required to be installed for Android Logcat to work.
 - Fix Memory Window on Android 11. Android 11 started dumping RSS memory, which was previously unexpected by Memory Window.
 - Include RSS memory in Memory window. 
 - Moved ProjectSettings\AndroidLogcatSettings.asset to UserSettings\AndroidLogcatSettings.asset, since this file wasn't meant to be commited. 
 - Fix various issues where logcat would incorrectly perform search on the phone below Android 7.0
 - Display the information in status bar by which data the messages are filtered, this should help with situtions where no messages are displayed logcat, only because a very specific tag or search filter is set.
 - Correctly get process name on Android 5.0 devices, previously processes with names like /system/bin/netd, would be resolved incorrectly.
 - You can now specify symbol path without CPU archtecture, if crash line will contain information about ABI, stacktrace utility will append the required CPU architecture.

### Changes
 - Memory window will be disabled by default, since it causes **Explicit concurrent copying GC freed** messages to be printed in the logcat which might unwanted behavior.
 - Remove automatic stacktrace resolving when receiving logcat messages, since in some cases it's impossible to automatically determine the correct symbol path, this creates a misleading behavior, where displayed stacktraces are incorrect. Please use Stacktrace Utility instead.

## [1.2.0] - 2020-08-18

### Fixes & Improvements.
 - Correctly resolve top activity when device is locked.
 - Improved Stacktrace Utility, it's easier to set symbol paths and regexes.
 - Consola font is now selectable in Logcat settings.
 - Android Logcat per project settings are saved in ProjectSettings directory.
 - Fix issue where sometimes Android Logcat would stop working if USB cable is unplugged and replugged.
 - Properly save/restore Android Logcat settings, previously settings like tags were being lost during domain reload or Editor restart.
 - Added Capture button in Capture Screen window, also capturing screen no longer will lock Unity thread.
 - Fix issue where incorrect date format in incoming log message would break whole log parsing.
 - Moved Stacktrace Utility, Capture Screen, Open Terminal under Tools menu.
 - The package list will automatically clean itself, if there's more than 5 exited packages in the list;
 - Improved documentation.
 - Added Clear button in internal log window.
 - Minimum Unity version was raised to 2019.2. The reason was to drop .NET 3.5 support.
 - 'Enter IP' window got renamed to 'Other connection options.
 - In the device selection list, you'll also able to see disconnected and unauthorized devices for informational purposes.
 - 'Other connection options' window has a Disconnect button for devices connected via Network.
 - Added device selection in Screen Capture window
### Memory Window
 - Introduced a window for viewing application memory in real time, more information in the docs.

## [1.1.1] - 2020-03-12

### Fixes & Improvements.
 - Fix warnings in scripts when active Editor platform is not Android.
 - Fix regex issues with logcat messages.

## [1.1.0] - 2020-02-14

### Fixes & Improvements.
 - Added feature 'Filter by process id'
 - Fixed addr2line functionality, when we try to resolve stacktrace
 - Correctly open Terminal on macOS Catalina
 - Fix Open Terminal button not working on Windows sometimes.
 - Reworked Connect to IP window, it's now multithreaded, thus it will not lock Editor. It's now easier to connect to Android device via IP.
 - Added icons for messages
 - Added disconnect button, you can stop logcat messages this way.
 - Improved mouse right click behavior to be consistent with the rest of Unity
 - Correctly identify Android 9 version
 - Android Settings will have color settings separated between Free skin and Pro skin.
 - Moved Android Settingsunder Preferences->Analysis
 - Provide proper windows title for Stacktrace Utility window
 - Right clicking log lines behavior will be consistent with other Unity windows.
 - Ctrl/CMD + C will copy log lines correctly.
 - Column seperators will be drawn correctly
### Android Logcat Settings 
 - Introducing settings, accessible from Preferences. For ex., on Windows Edit->Preferences
 
## [1.0.0] - 2019-07-18

### Fixes & Improvements.
- Added Open Terminal button
- Use monospace font for displaying log messages, this makes text align properly when displaying addresses
- Fix performance issues whene there's no Android device attached, the device querying will happen on worker thread.
- Fix Regex filter functionality, on newer devices it wasn't working as intended.
- Fix appearance of Delele button in Tag control window.
### Stacktrace Utility
- Add a custom way of resolving stacktraces, read more about in the documentation

## [0.2.7-preview] - 2019-04-24

### Minor fixes.
- Fixed the issues found during package validation.

## [0.2.6-preview] - 2019-04-15

### Minor fixes.
- Fixed some issues about tag window.
- Restored the states including current selected device, current selected package, tags, priority after closing and launching Unity Editor.

## [0.2.5-preview] - 2019-04-01

### Fixes & Improvements.
- UI improvements:
  - Add borders for columns.
  - Display odd/even background for message entries.
  - Fixed search bar issue.
  - Fix the issue that screenshot is cropped.
  - Add a simple tag control window to manipulate tags.
- Fixed the issue that search filter doesn't work on Android 6 or lower.
- Fixed the issue that tag filter doesn't work with some corner cases like "SSRM:k".
- Restored the states including current selected device, current selected package, tags, priority after closing and reopening the window.
- Fixed some issues in tags, like not allowing adding empty tag, setting tag length limitation to 23, etc.

## [0.2.1-preview] - 2019-01-22

### Fixes & Improvements.
- Made package compatible with .NET 3.5.
- Fixed the wrong year issue in the log message.
- Use "Cmd" instead of "Ctrl" for shortcuts on macOS.
- Fixed the issue that some log messages can't be parsed correctly.

## [0.2.0-preview] - 2018-12-18

### Fixes & Improvements.
- Show proper messages if the scripting runtime version is not .Net 4.x or the active platform is not Android.
- Added some shortcuts to copy, save logs etc.
- Fixed the "grep not fonud" issue on some old 4.x devices.

## [0.1.5-preview] - 2018-11-26

### Minor fixes.
- Fixed the issue during publishing the package.

## [0.1.4-preview] - 2018-11-26

### Minor fixes.
- Fixed the issue that "shift" selecting log messages doesn't work correctly.

## [0.1.3-preview] - 2018-11-16

### Fixes & Improvements.
- Fixed the issue that package filter doesn't work correctly on devices below Android 7.
- Fixed some issues at UI side.

## [0.1.2-preview] - 2018-10-30

### Minor fixes & Improvements.
- Added documentation.
- Fixed the issue that Android Logcat Package doesn't work with devices below Android 7.

## [0.1.1-preview] - 2018-10-22

### Minor fixes.

## [0.1.0-preview] - 2018-10-15

### This is the first release of *Android Logcat Package*.
