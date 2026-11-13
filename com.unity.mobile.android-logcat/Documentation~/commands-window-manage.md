# Manage and share commands

Run, manage, and share the Android Debug Bridge (adb) commands in the Commands window.

For a description of every control, refer to [Commands window reference](commands-window-reference.md). To add commands, refer to [Add a command](commands-window-add.md).

## Manage commands

You can reorder, edit, delete, or mark the commands as favorites by turning on **Edit Mode**. To delete several commands at once, select them in Edit Mode. For more information about each control, refer to [Command lists](commands-window-reference.md#command-lists).

## Run a command

To run a command on a device:

1. Connect the device. Refer to [Connect to a device](connect-to-a-device.md).
2. Select **Run** next to the command.
3. If the command contains placeholders, the [Resolve Placeholders dialog](commands-window-reference.md#resolve-placeholders-dialog) appears. Enter a value for each placeholder and then select **Run**.

The output panel displays the output. Only one command runs at a time. If a command contains multiple lines, each line runs as a separate command in order. If a command in the sequence fails, the output panel displays the error message and continues with the next command.

To stop a running command, select **Cancel**. The current command finishes and the remaining commands don't run. If an adb command runs when no device is selected, the sequence stops.

### Resolve placeholders

If a command contains placeholders, the **Resolve Placeholders** dialog opens when you select **Run**. A placeholder is text in angle brackets, such as `<package>`. Enter a value for each placeholder, then select **Run**. Unity prefills `<package>` and `<package/activity>` from the Player settings. For some placeholders, such as `<permission>`, you can choose a common value from a dropdown.

## Share commands

To share your commands, select **Export** to save them as a JSON file and share the file with other users. To use commands that someone shared with you, select **Import** and choose a JSON file.

Each command in the file needs both a name and a command. If the file contains no valid commands, Unity displays an error message.

> [!NOTE]
> Importing replaces all your current commands. Unity displays a warning to confirm before replacing them. To keep a backup of your commands, export them first.

## Additional resources

* [Commands window reference](commands-window-reference.md)
* [Add a command](commands-window-add.md)
* [Connect to a device](connect-to-a-device.md)
