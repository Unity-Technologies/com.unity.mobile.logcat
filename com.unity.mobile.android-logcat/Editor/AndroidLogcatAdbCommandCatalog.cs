namespace Unity.Android.Logcat
{
    internal static class AndroidLogcatAdbCommandCatalog
    {
        internal static readonly AndroidLogcatCommandEntry[] All = new[]
        {
            // --- Device Management ---
            new AndroidLogcatCommandEntry("List Devices", "adb devices"),
            new AndroidLogcatCommandEntry("List Devices (Verbose)", "adb devices -l"),
            new AndroidLogcatCommandEntry("Start Server", "adb start-server"),
            new AndroidLogcatCommandEntry("Kill Server", "adb kill-server"),
            new AndroidLogcatCommandEntry("Reboot Device", "adb reboot"),
            new AndroidLogcatCommandEntry("Reboot to Bootloader", "adb reboot bootloader"),
            new AndroidLogcatCommandEntry("Reboot to Recovery", "adb reboot recovery"),
            new AndroidLogcatCommandEntry("Get Device State", "adb get-state"),
            new AndroidLogcatCommandEntry("Get Serial Number", "adb get-serialno"),
            new AndroidLogcatCommandEntry("Wait for Device", "adb wait-for-device"),

            // --- App Management ---
            new AndroidLogcatCommandEntry("List All Packages", "adb shell pm list packages"),
            new AndroidLogcatCommandEntry("List Third-Party Packages", "adb shell pm list packages -3"),
            new AndroidLogcatCommandEntry("List System Packages", "adb shell pm list packages -s"),
            new AndroidLogcatCommandEntry("Clear App Data", "adb shell pm clear <package>"),
            new AndroidLogcatCommandEntry("Force Stop App", "adb shell am force-stop <package>"),
            new AndroidLogcatCommandEntry("Start Activity", "adb shell am start -n <package/activity>"),
            new AndroidLogcatCommandEntry("Start App (Launcher)", "adb shell monkey -p <package> -c android.intent.category.LAUNCHER 1"),
            new AndroidLogcatCommandEntry("Start App (UnityPlayerActivity)", "adb shell am start -n <package>/com.unity3d.player.UnityPlayerActivity"),
            new AndroidLogcatCommandEntry("Start App (GameActivity)", "adb shell am start -n <package>/com.google.androidgamesdk.GameActivity"),
            new AndroidLogcatCommandEntry("Uninstall App", "adb uninstall <package>"),
            new AndroidLogcatCommandEntry("Install APK", "adb install <path.apk>"),
            new AndroidLogcatCommandEntry("Install APK (Replace)", "adb install -r <path.apk>"),
            new AndroidLogcatCommandEntry("Install Multiple APKs (Split)", "adb install-multiple -r <path1.apk> <path2.apk>"),
            new AndroidLogcatCommandEntry("Push OBB File", "adb push <local.obb> /sdcard/Android/obb/<package>/"),
            new AndroidLogcatCommandEntry("Create OBB Directory", "adb shell mkdir -p /sdcard/Android/obb/<package>"),
            new AndroidLogcatCommandEntry("Grant Permission", "adb shell pm grant <package> <permission>"),
            new AndroidLogcatCommandEntry("Revoke Permission", "adb shell pm revoke <package> <permission>"),
            new AndroidLogcatCommandEntry("Dump App Info", "adb shell dumpsys package <package>"),

            // --- File Transfer ---
            new AndroidLogcatCommandEntry("Push File to Device", "adb push <local> <remote>"),
            new AndroidLogcatCommandEntry("Pull File from Device", "adb pull <remote> <local>"),
            new AndroidLogcatCommandEntry("List Directory", "adb shell ls -la <path>"),
            new AndroidLogcatCommandEntry("Remove File", "adb shell rm <path>"),
            new AndroidLogcatCommandEntry("Make Directory", "adb shell mkdir -p <path>"),

            // --- Logcat ---
            new AndroidLogcatCommandEntry("View Logcat", "adb logcat"),
            new AndroidLogcatCommandEntry("Clear Logcat", "adb logcat -c"),
            new AndroidLogcatCommandEntry("Dump Logcat & Exit", "adb logcat -d"),
            new AndroidLogcatCommandEntry("Logcat Errors Only", "adb logcat *:E"),
            new AndroidLogcatCommandEntry("Logcat Warnings & Above", "adb logcat *:W"),
            new AndroidLogcatCommandEntry("Logcat with Timestamps", "adb logcat -v time"),
            new AndroidLogcatCommandEntry("Logcat by Tag", "adb logcat -s <TAG>"),
            new AndroidLogcatCommandEntry("Logcat Unity Only", "adb logcat -s Unity"),

            // --- Screen Capture ---
            new AndroidLogcatCommandEntry("Take Screenshot", "adb shell screencap /sdcard/screenshot.png"),
            new AndroidLogcatCommandEntry("Record Screen", "adb shell screenrecord /sdcard/recording.mp4"),
            new AndroidLogcatCommandEntry("Record Screen (30s)", "adb shell screenrecord --time-limit 30 /sdcard/recording.mp4"),

            // --- System Info ---
            new AndroidLogcatCommandEntry("Get Android Version", "adb shell getprop ro.build.version.release"),
            new AndroidLogcatCommandEntry("Get SDK Version", "adb shell getprop ro.build.version.sdk"),
            new AndroidLogcatCommandEntry("Get Device Model", "adb shell getprop ro.product.model"),
            new AndroidLogcatCommandEntry("Get Device Manufacturer", "adb shell getprop ro.product.manufacturer"),
            new AndroidLogcatCommandEntry("Get All Properties", "adb shell getprop"),
            new AndroidLogcatCommandEntry("Get Screen Resolution", "adb shell wm size"),
            new AndroidLogcatCommandEntry("Get Screen Density", "adb shell wm density"),
            new AndroidLogcatCommandEntry("Get Battery Info", "adb shell dumpsys battery"),
            new AndroidLogcatCommandEntry("Get CPU Info", "adb shell cat /proc/cpuinfo"),
            new AndroidLogcatCommandEntry("Get Memory Info", "adb shell cat /proc/meminfo"),
            new AndroidLogcatCommandEntry("Get Disk Usage", "adb shell df"),
            new AndroidLogcatCommandEntry("Get Running Processes", "adb shell ps"),
            new AndroidLogcatCommandEntry("Get Top Processes", "adb shell top -n 1"),
            new AndroidLogcatCommandEntry("Get Build Properties", "adb shell cat /system/build.prop"),

            // --- Input Simulation ---
            new AndroidLogcatCommandEntry("Send Keyevent (Home)", "adb shell input keyevent KEYCODE_HOME"),
            new AndroidLogcatCommandEntry("Send Keyevent (Back)", "adb shell input keyevent KEYCODE_BACK"),
            new AndroidLogcatCommandEntry("Send Keyevent (Menu)", "adb shell input keyevent KEYCODE_MENU"),
            new AndroidLogcatCommandEntry("Send Keyevent (Power)", "adb shell input keyevent KEYCODE_POWER"),
            new AndroidLogcatCommandEntry("Send Keyevent (Volume Up)", "adb shell input keyevent KEYCODE_VOLUME_UP"),
            new AndroidLogcatCommandEntry("Send Keyevent (Volume Down)", "adb shell input keyevent KEYCODE_VOLUME_DOWN"),
            new AndroidLogcatCommandEntry("Send Keyevent (Enter)", "adb shell input keyevent KEYCODE_ENTER"),
            new AndroidLogcatCommandEntry("Send Keyevent (Tab)", "adb shell input keyevent KEYCODE_TAB"),
            new AndroidLogcatCommandEntry("Send Text", "adb shell input text <text>"),
            new AndroidLogcatCommandEntry("Tap Screen", "adb shell input tap <x> <y>"),
            new AndroidLogcatCommandEntry("Swipe Screen", "adb shell input swipe <x1> <y1> <x2> <y2>"),

            // --- Networking ---
            new AndroidLogcatCommandEntry("Enable WiFi ADB", "adb tcpip 5555"),
            new AndroidLogcatCommandEntry("Connect via IP", "adb connect <ip>:5555"),
            new AndroidLogcatCommandEntry("Disconnect All", "adb disconnect"),
            new AndroidLogcatCommandEntry("Forward Port", "adb forward tcp:<local> tcp:<remote>"),
            new AndroidLogcatCommandEntry("Reverse Port", "adb reverse tcp:<remote> tcp:<local>"),
            new AndroidLogcatCommandEntry("Get IP Address", "adb shell ip addr show wlan0"),
            new AndroidLogcatCommandEntry("Ping Host", "adb shell ping -c 4 <host>"),

            // --- Dumpsys ---
            new AndroidLogcatCommandEntry("Dump Activity Stack", "adb shell dumpsys activity activities"),
            new AndroidLogcatCommandEntry("Dump Memory Info", "adb shell dumpsys meminfo"),
            new AndroidLogcatCommandEntry("Dump Window Info", "adb shell dumpsys window"),
            new AndroidLogcatCommandEntry("Dump Display Info", "adb shell dumpsys display"),
            new AndroidLogcatCommandEntry("Dump CPU Info", "adb shell dumpsys cpuinfo"),
            new AndroidLogcatCommandEntry("Dump Battery Stats", "adb shell dumpsys batterystats"),
            new AndroidLogcatCommandEntry("Dump SurfaceFlinger", "adb shell dumpsys SurfaceFlinger"),
            new AndroidLogcatCommandEntry("Dump Graphics Stats", "adb shell dumpsys gfxinfo <package>"),
            new AndroidLogcatCommandEntry("Dump Wifi Info", "adb shell dumpsys wifi"),
            new AndroidLogcatCommandEntry("Dump Alarm Info", "adb shell dumpsys alarm"),
            new AndroidLogcatCommandEntry("Dump Notification Info", "adb shell dumpsys notification"),

            // --- Settings ---
            new AndroidLogcatCommandEntry("Enable Stay Awake", "adb shell settings put global stay_on_while_plugged_in 3"),
            new AndroidLogcatCommandEntry("Disable Stay Awake", "adb shell settings put global stay_on_while_plugged_in 0"),
            new AndroidLogcatCommandEntry("Show Touches On", "adb shell settings put system show_touches 1"),
            new AndroidLogcatCommandEntry("Show Touches Off", "adb shell settings put system show_touches 0"),
            new AndroidLogcatCommandEntry("Enable USB Debugging", "adb shell settings put global adb_enabled 1"),
            new AndroidLogcatCommandEntry("Set Screen Off Timeout", "adb shell settings put system screen_off_timeout <ms>"),

            // --- Debug / Advanced ---
            new AndroidLogcatCommandEntry("Bug Report", "adb bugreport"),
            new AndroidLogcatCommandEntry("Remount System", "adb remount"),
            new AndroidLogcatCommandEntry("Root Shell", "adb root"),
            new AndroidLogcatCommandEntry("Unroot Shell", "adb unroot"),
            new AndroidLogcatCommandEntry("Disable Verity", "adb disable-verity"),
            new AndroidLogcatCommandEntry("Enable Verity", "adb enable-verity"),
            new AndroidLogcatCommandEntry("Wait for Device Boot", "adb wait-for-device shell getprop sys.boot_completed"),
            new AndroidLogcatCommandEntry("List Tombstones", "adb shell ls -lt /data/tombstones/"),
            new AndroidLogcatCommandEntry("Pull Tombstone", "adb pull /data/tombstones/<tombstone_XX> <local_path>"),

            // --- Meta Quest / XR ---
            new AndroidLogcatCommandEntry("List OVR Packages", "adb shell pm list packages | grep oculus"),
            new AndroidLogcatCommandEntry("Get Guardian State", "adb shell dumpsys OVRGuardianService"),
            new AndroidLogcatCommandEntry("Get Compositor Stats", "adb shell dumpsys OVRCompositor"),
            new AndroidLogcatCommandEntry("Set GPU Level", "adb shell setprop debug.oculus.gpuLevel <0-4>"),
            new AndroidLogcatCommandEntry("Set CPU Level", "adb shell setprop debug.oculus.cpuLevel <0-4>"),
            new AndroidLogcatCommandEntry("Enable Perf Overlay", "adb shell setprop debug.oculus.enablePerfOverlay 1"),
            new AndroidLogcatCommandEntry("Disable Perf Overlay", "adb shell setprop debug.oculus.enablePerfOverlay 0"),
            new AndroidLogcatCommandEntry("Set Fixed Foveation", "adb shell setprop debug.oculus.foveation.level <0-4>"),
            new AndroidLogcatCommandEntry("Set Refresh Rate", "adb shell setprop debug.oculus.refreshRate <72|90|120>"),

            // --- AAB / Bundletool ---
            new AndroidLogcatCommandEntry("bundletool: Build Split APKs", "java -jar bundletool.jar build-apks --bundle=<path.aab> --output=<output.apks> --connected-device"),
            new AndroidLogcatCommandEntry("bundletool: Install APKs", "java -jar bundletool.jar install-apks --apks=<path.apks>"),
            new AndroidLogcatCommandEntry("bundletool: Get Device Spec", "java -jar bundletool.jar get-device-spec --output=<device-spec.json>"),
        };
    }
}
