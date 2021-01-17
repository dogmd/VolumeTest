using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VolumeMixerDesktop {
    /// <summary>
    /// 4 columns that specify the idType and match settings for the 4 knobs.
    /// A multiselect that specifies which unit to recieve commands from.
    /// </summary
    public partial class MainWindow : Window {
        public static HotKey VolumeUpCmd;
        public static HotKey VolumeDownCmd;
        public static HotKey ToggleMuteCmd;
        private static MatchOption FocusedProcess;
        public MainWindow() {
            InitializeComponent();
            WebServer ws = new WebServer();
            availableListReload(null, null);
        }

        private void availableListReload(object sender, RoutedEventArgs e) {
            List<MatchOption> matchItems = new List<MatchOption>();
            foreach (var process in Process.GetProcesses()) {
                if (VolumeMixerInterFace.GetApplicationVolume(process.Id) != null) {
                    matchItems.Add(new MatchOption { Display = process.ProcessName + " - " + process.Id, Process = process });
                }
            }
            if (availableList != null) {
                matchItems.Sort((a, b) => a.Display.CompareTo(b.Display));
                availableList.ItemsSource = matchItems;
            }
        }

        // FROM https://stackoverflow.com/a/10280800/4352298

        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint EVENT_SYSTEM_FOREGROUND = 3;

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime) {
            GetWindowThreadProcessId(hwnd, out uint pid);
            Process focused = Process.GetProcessById((int)pid);
            foreach (MatchOption match in availableList.Items) {
                if (match.Process.ProcessName == focused.ProcessName) {
                    FocusedProcess = match;
                    reloadBound();
                    return;
                } 
            }
            reloadBound();
            FocusedProcess = null;
        }

        // ======================================================

        private void IncrementCheckNumeric(object sender, TextCompositionEventArgs e) {
            e.Handled = !e.Text.Any(x => Char.IsDigit(x) || '.'.Equals(x));
        }

        private void availableChanged(object sender, SelectionChangedEventArgs e) {
            reloadBound();
        }

        // FROM https://stackoverflow.com/questions/2136431/how-do-i-read-custom-keyboard-shortcut-from-user-in-wpf#
        private void ShortcutTextBox_PreviewKeyDown(object sender, KeyEventArgs e) {
            // The text box grabs all input.
            e.Handled = true;

            // Fetch the actual shortcut key.
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Ignore modifier keys.
            if (key == Key.LeftShift || key == Key.RightShift
                || key == Key.LeftCtrl || key == Key.RightCtrl
                || key == Key.LeftAlt || key == Key.RightAlt
                || key == Key.LWin || key == Key.RWin) {
                return;
            }

            // Build the shortcut key name.
            StringBuilder shortcutText = new StringBuilder();
            KeyModifier modifiers = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) {
                shortcutText.Append("Ctrl+");
                modifiers |= KeyModifier.Ctrl;
            }
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) {
                shortcutText.Append("Shift+");
                modifiers |= KeyModifier.Shift;
            }
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) {
                shortcutText.Append("Alt+");
                modifiers |= KeyModifier.Alt;
            }
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) {
                shortcutText.Append("Win+");
                modifiers |= KeyModifier.Win;
            }
            shortcutText.Append(key.ToString());

            // Update the text box.
            TextBox _ShortcutTextBox = (TextBox)sender;

            if (_ShortcutTextBox.Name == "volUpInput") {
                if (VolumeUpCmd != null) {
                    VolumeUpCmd.Unregister();
                }
                VolumeUpCmd = new HotKey(key, modifiers, VolumeUp);
            } else if (_ShortcutTextBox.Name == "volDownInput") {
                if (VolumeDownCmd != null) {
                    VolumeDownCmd.Unregister();
                }
                VolumeDownCmd = new HotKey(key, modifiers, VolumeDown);
            } else if (_ShortcutTextBox.Name == "toggleMuteInput") {
                if (ToggleMuteCmd != null) {
                    ToggleMuteCmd.Unregister();
                }
                ToggleMuteCmd = new HotKey(key, modifiers, ToggleMute);
            }

            _ShortcutTextBox.Text = shortcutText.ToString();
        }

        private List<MatchOption> reloadBound() {
            List<MatchOption> bound = new List<MatchOption>();
            foreach (MatchOption match in availableList.SelectedItems) {
                bound.Add(match);
            }
            if (FocusedProcess != null && (bool)focusedCheckBox.IsChecked && !bound.Contains(FocusedProcess)) {
                bound.Add(FocusedProcess);
            }
            boundList.ItemsSource = bound;
            return bound;
        }

        private void ChangeVolume(float step) {
            foreach (MatchOption match in reloadBound()) {
                VolumeMixerInterFace.IncrementApplicationVolume(match.Process.Id, step);
            }
        }

        private void VolumeUp(HotKey hotKey) {
            float step = float.TryParse(volStepInput.Text, out step) ? step : 5;
            ChangeVolume(step);
        }

        private void VolumeDown(HotKey hotKey) {
            float step = float.TryParse(volStepInput.Text, out step) ? -1 * step : -5;
            ChangeVolume(step);
        }

        private void ToggleMute(HotKey hotKey) {
            foreach (MatchOption match in reloadBound()) {
                VolumeMixerInterFace.ToggleApplicationMute(match.Process.Id);
            }
        }
    }

    public class MatchOption {
        public string Display { get; set; }

        public Process Process { get; set; }

        public List<int> pids { get; set; }
    }
}
