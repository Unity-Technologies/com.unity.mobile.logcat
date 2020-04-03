### Stacktrace Utility
Allows you to copy paste custom log and resolve stacktrace.

Quick Steps:
- Copy paste a crash log from the application logcat.
- Set symbol path for specific **Configuration/Scripting Backend/CPU**
  - For ex., if crash log contains the following line:
    - **2019-05-17 12:00:58.823 30759-30803/? E/CRASH: Build type 'Release', Scripting Backend 'mono', CPU 'armeabi-v7a'"**
  - The build will be **Release/mono/armeabi-v7a**
  - The symbol path will be **Unity_Version/Editor/Data/PlaybackEngines/AndroidPlayer/Variations/mono/Release/Symbols/armeabi-v7a**
- (Optional)Adjust address resolving regex, the default regex is set to resolved addresses from following line:
  - **#00  pc 002983fc  /data/app/air.com.games2win.internationalfashionstylist-K3NlW-1enTfyTaSF59VaHA==/lib/arm/libunity.so**
  - You should adjust the regex if your addresses are printed in a different format
- Click Resolve Stacktraces

![Device Screen Capture](images/stacktraceUtility.png)

**Note: If you provide invalid symbol path, the function names will still be resolved, but they will not be correct. Android tools don't validate if specific address belongs to specific symbol file.**
