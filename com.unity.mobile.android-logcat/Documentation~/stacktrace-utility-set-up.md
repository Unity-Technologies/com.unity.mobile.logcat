# Set up Stacktrace Utility

Before you use the Stacktrace Utility tool, you must provide it with the resources it requires to resolve stacktraces. This page explains to set up the Stacktrace Utility tool.

## Configure symbol paths

To resolve stacktrace for a Unity application, the Stacktrace Utility tool requires paths to the `libmain`, `libunity`, and `libil2cpp` [symbol files](https://docs.unity3d.com/Manual/android-symbols.html). To configure these paths:

1. Open the [Stacktrace Utility window](stacktrace-utility-window-reference.md).
2. From the [Stacktrace Utility controls](stacktrace-utility-window-reference.md#stacktrace-utility-controls) section, select **Configure Symbol Paths**. This opens the Android Logcat Settings section of the [Project Settings window](https://docs.unity3d.com/Manual/comp-ManagerGroup.html).
3. In the Project Settings window, select **Add** > **Pick Custom Location**.
4. Find the `libmain`, `libunity`, and `libil2cpp` symbol files and add the folder that each of them are within. Different versions of Unity produce these symbol files in different locations. For information on where to find these symbols, see the [Android symbols](https://docs.unity3d.com/Manual/android-symbols.html) documentation for your version of Unity.

> [!NOTE]
> You can specify the symbol path without CPU architecture. For example, `...AndroidPlayer\Variations\il2cpp\Development\Symbols` instead of `...AndroidPlayer\Variations\il2cpp\Development\Symbols\arm64-v8a`. In this case, the tool tries to calculate the CPU architecture and appends the missing subfolder. This is only possible if the crash line has information about the [application binary interface](https://developer.android.com/ndk/guides/abis). For example: `2020/10/30 16:38:32.985 19060 19081 Error AndroidRuntime #3 pc 000000000019a6f8 /data/app/~~Ctx1WDf6mhlw6jvONDIlaQ==/com.DefaultCompany.ForceCrash-2XwBC-UrBxWJCR2aVVdY1A==/lib/arm64/libil2cpp.so`

When the Stacktrace Utility tool resolves stacktraces, it iterates through the symbol path list and uses the first located version of each symbol file. If you use more than one symbol path, be aware of symbol files with the same file name in multiple folders, because the tool only uses the first one it finds.

> [!NOTE]
> If you provide an invalid symbol path, the Stacktrace Utility tool still resolves function names, but they will not be correct. Android tools don't validate if a specific address belongs to a specific symbol file.

## Configure stacktrace regular expressions

The Stacktrace Utility tool uses regular expressions to parse entries. By default, the tool contains regular expressions that parse the address and library name from each entry, but you can add your own regular expressions to resolve addresses and library names.

To add your own regular expressions:

1. Open the [Stacktrace Utility window](stacktrace-utility-window-reference.md).
2. From the [Stacktrace Utility controls](stacktrace-utility-window-reference.md#stacktrace-utility-controls) section, select **Configure Regex**. This opens the Android Logcat Settings section of the [Preferences window](https://docs.unity3d.com/Manual/Preferences.html).
3. In the Preferences window, find **Stacktrace Regex**.
4. In the text input field, enter your regular expression and click **Add**.

> [!IMPORTANT]
> Make sure to use the `libName` and `address` capture groups in your regular expressions.

## Additional resources

* [Resolve a stacktrace](stacktrace-utility-resolve.md)