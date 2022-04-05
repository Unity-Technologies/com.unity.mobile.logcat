# Filter the message log

You can filter the message log so that it only displays the messages that match the filter. You can filter by:

* [Message text](#filter-by-text)
* [Priority](#filter-by-priority)
* [Tag](#filter-by-tag)
* [Process ID](#filter-by-process-id)

## Filter by text

To filter the message log by text, use **Filter Input** and **Filter Options** in the [toolbar](android-logcat-window-reference.md#toolbar). The Android Logcat window checks the text in **Filter Input** against the text in each message and only displays messages that contain the filter text. **Filter Options** determine whether the filter text is case-sensitive and also whether to treat the filter as a regular expression.

## Filter by priority

To filter the message log by priority, right-click the **Priority** column header in the [message log](android-logcat-window-reference.md#message-log) and select the priorities you want to appear in the message log. For information about the types of priority, see [Filtering log output](https://developer.android.com/studio/command-line/logcat#filteringOutput).

## Filter by tag

Tags indicate the origin of the log message. To filter messages by tag, right-click the **Tag** column header in the message log and select the tags you want to appear in the message log. 

## Filter by process ID

To filter the message log by process ID, right-click the **PID** column header in the message log and select **Filter by process id**.

## Additional resources

* [Message log](android-logcat-window-reference.md#message-log).
* [Customize message log columns](android-logcat-window-message-log-customize).