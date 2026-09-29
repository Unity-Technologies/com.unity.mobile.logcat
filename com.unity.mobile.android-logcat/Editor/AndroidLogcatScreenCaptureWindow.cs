using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEditor.ShortcutManagement;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatScreenCaptureWindow : EditorWindow
    {
        class Styles
        {
            static readonly GUIContent kMore = EditorGUIUtility.IconContent("_Menu");
            public static GUIContent Advanced = kMore != null && kMore.image != null
                ? new GUIContent(kMore.image)
                : new GUIContent("...");

            public static GUIContent TakeScreenshot = new GUIContent("Take Screenshot",
                "Capture the device screen and add it to the list. The screenshot comes from the device "
                + "rather than from the stream, so it is full resolution whatever the stream is scaled to. "
                + "Shortcut: Ctrl+Shift+S, Cmd+Shift+S on macOS.");
            public static GUIContent TakeRecording = new GUIContent("Take Recording",
                "Record the device screen while watching it. The recording is taken on the device "
                + "and is added to the list when it stops. Leaving the live view stops it.");
            public static GUIContent StopRecording = new GUIContent("Stop Recording",
                "Stop recording and add the recording to the list.");
        }
        private AndroidLogcatRuntimeBase m_Runtime;

        private const int kButtonAreaHeight = 30;

        private AndroidLogcatCaptureScreenshot m_CaptureScreenshot;
        private AndroidLogcatCaptureVideo m_CaptureVideo;
        private AndroidLogcatLiveStream m_LiveStream;

        private AndroidLogcatDeviceSelection m_DeviceSelection;
        private IAndroidLogcatDevice m_LastDeviceUsedForAssets;

        private AndroidLogcatScreenshotList m_ScreenshotList;
        private AndroidLogcatStatusBar m_StatusBar;

        private AndroidLogcatLiveStream.CaptureAction[] m_CaptureActions;

        const float kAdvancedMenuWidth = 26;

        /// <summary>Anything the device is doing for this window, for the progress icon.</summary>
        private bool IsCapturing =>
            m_CaptureScreenshot.IsCapturing || m_CaptureVideo.IsRecording || m_LiveStream.IsStreaming;

        // Alongside the Logcat window's own entry, and reachable without opening that
        // window first - the Screen Capture window is useful on its own. A device with
        // no Android support installed gets the same message here as anywhere else, from
        // OnGUI, rather than the item being hidden.
        [MenuItem("Window/Analysis/Android Screen Capture")]
        public static void ShowWindow()
        {
            GetWindow<AndroidLogcatScreenCaptureWindow>("Device Screen Capture");
        }

        private void OnEnable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_DeviceSelection = new AndroidLogcatDeviceSelection(m_Runtime, ReloadCaptureAssetsIfNeeded, nameof(AndroidLogcatScreenCaptureWindow) + "_DeviceId");
            m_Runtime.Closing += OnDisable;
            m_CaptureScreenshot = m_Runtime.CaptureScreenshot;
            m_CaptureVideo = m_Runtime.CaptureVideo;
            m_LiveStream = m_Runtime.LiveStream;
            m_LiveStream.StreamChanged += ReportStream;
            m_ScreenshotList = new AndroidLogcatScreenshotList(m_Runtime, Repaint);
            // Nothing here connects to anything, so the bar carries the message alone.
            m_StatusBar = new AndroidLogcatStatusBar() { ShowConnection = false };

            // The buttons the live view draws for this window, in the order they
            // appear. Each says for itself when it can run.
            m_CaptureActions = new[]
            {
                new AndroidLogcatLiveStream.CaptureAction(() => Styles.TakeScreenshot,
                    QueueScreenCapture, () => CanCaptureScreenshot),
                // One button that starts and stops: a recording is running or it is
                // not, and two buttons would leave one of them dead most of the time.
                new AndroidLogcatLiveStream.CaptureAction(
                    () => m_CaptureVideo.IsRecording ? Styles.StopRecording : Styles.TakeRecording,
                    ToggleRecording, () => CanRecord)
            };

            m_Runtime.DeviceQuery.UpdateConnectedDevicesList(true);
        }
        private void ReloadCaptureAssetsIfNeeded(IAndroidLogcatDevice device)
        {
            if (m_LastDeviceUsedForAssets == device)
                return;

            m_LastDeviceUsedForAssets = device;

            // The captures are not tied to a device, so losing one keeps the view.
            if (string.IsNullOrEmpty(m_Runtime.CaptureScreenshot.SelectedImagePath))
                m_Runtime.CaptureScreenshot.LoadImage(m_Runtime.CaptureScreenshot.GetLatestImagePath(device));

            m_ScreenshotList.OnDeviceChanged(m_DeviceSelection.SelectedDevice);
        }

        private void OnDisable()
        {
            // The live stream and the recorder are owned by the runtime, so they would
            // otherwise carry on after the window that was showing them is gone - and
            // keep reporting to a status bar that is gone with it. Dropping the
            // selection ends both.
            m_ScreenshotList?.Deselect();

            if (m_LiveStream != null)
            {
                m_LiveStream.StreamChanged -= ReportStream;
                m_LiveStream = null;
            }

            m_ScreenshotList?.Dispose();

            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            if (m_Runtime == null)
                return;
            m_DeviceSelection.Dispose();
            m_DeviceSelection = null;
            m_Runtime = null;
        }

        /// <summary>Whether a screenshot can be taken right now.</summary>
        private bool CanCaptureScreenshot =>
            m_DeviceSelection.SelectedDevice != null && !m_CaptureScreenshot.IsCapturing;

        /// <summary>
        /// Whether a recording can be started or stopped. Stopping is always allowed
        /// once one is running, whatever happened to the device since.
        /// </summary>
        private bool CanRecord =>
            m_CaptureVideo.IsRecording || m_DeviceSelection.SelectedDevice != null;

        private void ToggleRecording()
        {
            if (m_CaptureVideo.IsRecording)
            {
                m_CaptureVideo.StopRecording();
                return;
            }

            // Whatever the bar said about the last capture is about to be out of date.
            m_StatusBar.Message = string.Empty;

            var settings = m_Runtime.Settings;
            m_CaptureVideo.StartRecording(m_DeviceSelection.SelectedDevice, OnVideoCompleted,
                settings.VideoTimeLimitEnabled ? TimeSpan.FromSeconds(settings.VideoTimeLimit) : (TimeSpan?)null,
                settings.VideoSizeEnabled ? settings.VideoSizeX : (uint?)null,
                settings.VideoSizeEnabled ? settings.VideoSizeY : (uint?)null,
                settings.VideoBitRateEnabled ? settings.VideoBitRateK * 1000 : (ulong?)null);
        }

        private void QueueScreenCapture()
        {
            // Whatever the bar said about the last one is about to be out of date.
            m_StatusBar.Message = string.Empty;
            m_CaptureScreenshot.QueueScreenCapture(m_DeviceSelection.SelectedDevice, OnScreenshotCompleted);
        }

        /// <summary>
        /// Says what the stream is doing, whenever it has something new to say - see
        /// <see cref="AndroidLogcatLiveStream.StreamChanged"/>.
        /// </summary>
        private void ReportStream()
        {
            var stream = m_LiveStream.StreamSize;

            if (!m_LiveStream.IsStreaming)
            {
                // The sizes outlive the stream, so a stream that ended having never
                // delivered a frame has nothing here. It failed, and the view says so
                // where the image would be.
                if (stream.x > 0)
                    m_StatusBar.Message = "Live stream stopped";
            }
            else
            {
                var display = m_LiveStream.DisplaySize;
                var scaledFrom = display.x > 0 ? $"{display.x}x{display.y} scaled to " : string.Empty;
                var device = m_DeviceSelection.SelectedDevice;
                var name = device != null ? device.ShortDisplayName : "device";

                m_StatusBar.Message = $"Live stream: {name}, {scaledFrom}{stream.x}x{stream.y}, " +
                    $"up to {m_Runtime.Settings.LiveStreamMaxFps} fps";
            }

            Repaint();
        }

        /// <summary>
        /// Says where a capture landed, in the status bar. A path inside the project
        /// is shown relative to it, which is short enough to read at a glance.
        /// </summary>
        private void ReportSaved(string what, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                m_StatusBar.Message = string.Empty;
                return;
            }

            var full = Path.GetFullPath(path).Replace("\\", "/");
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace("\\", "/") + "/";
            var shown = full.StartsWith(project, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(project.Length)
                : full;

            m_StatusBar.Message = $"{what} saved to '{shown}'";
        }

        /// <summary>
        /// Ctrl+Shift+S, and Cmd+Shift+S on macOS - <see cref="ShortcutModifiers.Action"/>
        /// is whichever of the two the platform uses.
        /// <para>
        /// Scoped to this window rather than registered globally: the Editor's own
        /// File > Save As sits on the same chord, and a window scoped shortcut takes
        /// precedence over a global one only while its window has focus. It shows up in
        /// Edit > Shortcuts under "Android Logcat", so it can be rebound there.
        /// </para>
        /// </summary>
        [Shortcut("Android Logcat/Capture Screenshot", typeof(AndroidLogcatScreenCaptureWindow),
            KeyCode.S, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        static void CaptureScreenshotShortcut(ShortcutArguments args)
        {
            var window = args.context as AndroidLogcatScreenCaptureWindow;
            if (window != null)
                window.CaptureScreenshotFromShortcut();
        }

        void CaptureScreenshotFromShortcut()
        {
            // The same conditions the button draws itself with: no device, or a
            // capture already in flight.
            if (m_Runtime == null || m_DeviceSelection == null)
                return;
            if (!CanCaptureScreenshot)
                return;

            QueueScreenCapture();
        }

        void OnScreenshotCompleted()
        {
            // The image lands on disk while the capture is still running, and its
            // details file only when the capture is integrated here. Selecting the row
            // in between loads one without the other, and the preview would keep that
            // for as long as the selection does not change.
            m_ScreenshotList?.InvalidatePreview();

            // Set after the capture was integrated, so this is the new screenshot -
            // and empty when the capture failed, where the error is reported already.
            ReportSaved("Screenshot", m_CaptureScreenshot.SelectedImagePath);

            var texture = m_CaptureScreenshot.ImageTexture;
            if (texture != null)
                maxSize = new Vector2(Math.Max(texture.width, position.width), texture.height + kButtonAreaHeight);
            Repaint();
        }

        void OnVideoCompleted(AndroidLogcatCaptureVideo.Result result, string videoPath)
        {
            var recorded = result == AndroidLogcatCaptureVideo.Result.Success;
            ReportSaved("Recording", recorded ? videoPath : null);

            // The list is counted from a cached scan, which knows nothing of a file
            // the recorder has just written.
            m_CaptureScreenshot.InvalidateScreenshots();
            if (recorded)
                m_ScreenshotList.Select(videoPath);

            Repaint();
        }

        /// <summary>
        /// The screenshots folder is an ordinary directory that the user can add to,
        /// delete from or overwrite behind the Editor's back. Nothing inside the Editor
        /// can notice that, so the listing and the loaded image are both dropped when
        /// this window comes back to the front - the moment someone is most likely to
        /// have just been doing exactly that in a file browser.
        /// </summary>
        void OnFocus()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled || m_Runtime == null)
                return;

            m_CaptureScreenshot.InvalidateScreenshots();
            m_ScreenshotList?.InvalidatePreview();
            Repaint();
        }

        void OnGUI()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
            {
                AndroidLogcatUtilities.ShowAndroidIsNotInstalledMessage();
                return;
            }

            EditorGUILayout.BeginVertical();
            GUILayout.Space(5);

            DoToolbarGUI();

            GUILayout.Space(5);
            DoPreviewGUI();

            m_StatusBar?.DoGUI();

            EditorGUILayout.EndVertical();
        }

        private void DoToolbarGUI()
        {
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);

            DoProgressGUI();
            m_DeviceSelection.DoGUI();

            GUILayout.FlexibleSpace();
            DoAdvancedMenuGUI();

            EditorGUILayout.EndHorizontal();

            // The line along the bottom of the toolbar is drawn by the controls, not by
            // the toolbar - its own background is shorter than this row - so it stops
            // wherever the row is empty. One line across the row closes it.
            var toolbarRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(toolbarRect.x, toolbarRect.yMax - 1, toolbarRect.width, 1),
                    EditorGUIUtility.isProSkin ? new Color(0.14f, 0.14f, 0.14f) : new Color(0.6f, 0.6f, 0.6f));
            }
        }

        /// <summary>
        /// The settings this window has, reached the way the Package Manager's toolbar
        /// reaches its own: a menu at the right hand end of the toolbar.
        /// </summary>
        private void DoAdvancedMenuGUI()
        {
            var rect = GUILayoutUtility.GetRect(Styles.Advanced, AndroidLogcatStyles.toolbarButton,
                GUILayout.Width(kAdvancedMenuWidth));
            if (!GUI.Button(rect, Styles.Advanced, AndroidLogcatStyles.toolbarButton))
                return;

            var menu = new GenericMenu();
            menu.AddItem(EditorGUIUtility.TrTextContent("Preferences"), false,
                () => SettingsService.OpenUserPreferences(AndroidLogcatSettingsProvider.kSettingsPath));
            menu.DropDown(rect);
        }

        private void DoProgressGUI()
        {
            AndroidLogcatUtilities.DrawProgressIcon(IsCapturing);
            if (IsCapturing)
                Repaint();
        }

        private void DoScreenshotGUI(Rect rc)
        {
            // Drawn with or without a device: these are files on this machine, and
            // they outlive the device they came from. What needs a device - Capture,
            // the live view - disables itself.
            // The list draws itself and the splitter, and hands back what is left.
            var imageRect = m_ScreenshotList.DoGUI(rc, m_DeviceSelection.SelectedDevice);

            if (m_ScreenshotList.LiveSelected)
            {
                // The developer-mode details are drawn by DoGUI, in the info column.
                m_LiveStream.DoGUI(imageRect, m_DeviceSelection.SelectedDevice, Repaint, m_CaptureActions);
                // Frames arrive on the runtime's update, not on GUI events, so the window
                // has to keep repainting to show them.
                if (m_LiveStream.IsStreaming)
                    Repaint();
            }
            // The list draws the image, not AndroidLogcatCaptureScreenshot: its texture
            // is the last capture rather than the selected row.
            else if (!m_ScreenshotList.DoPreviewGUI(imageRect))
            {
                var message = m_DeviceSelection.SelectedDevice == null
                    ? "No capture to show. Select one from the list."
                    : "No capture to show. Select Live, then Take Screenshot or Take Recording.";
                EditorGUI.HelpBox(imageRect, message, MessageType.Info);
            }
        }

        private void DoPreviewGUI()
        {
            // Claimed from the layout rather than offset by a hardcoded toolbar
            // height, which left a gap when the two disagreed.
            var rc = GUILayoutUtility.GetRect(0, 0,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DoScreenshotGUI(rc);
        }

    }
}
