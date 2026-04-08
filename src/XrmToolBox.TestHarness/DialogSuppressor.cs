using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace XrmToolBox.TestHarness
{
    /// <summary>
    /// Polls for Win32 MessageBox dialogs (#32770) owned by this process and
    /// auto-dismisses them, logging the dialog text to stderr. Used with
    /// --suppress-dialogs to prevent modal dialogs from blocking FlaUI UIA operations.
    ///
    /// File dialogs (Open/Save) also use #32770 but are distinguished by their
    /// child control classes (ComboBoxEx32, ToolbarWindow32, etc.) and are NOT dismissed.
    /// </summary>
    public sealed class DialogSuppressor : IDisposable
    {
        private readonly int _currentProcessId;
        private readonly Timer _timer;
        private readonly HashSet<IntPtr> _dismissed = new HashSet<IntPtr>();
        private readonly string _screenshotDir;
        private bool _disposed;

        // Child control classes that indicate a common file dialog, not a MessageBox.
        // File dialogs contain explorer shell controls that MessageBoxes never have.
        private static readonly string[] FileDialogChildClasses =
        {
            "ComboBoxEx32",      // filename/path combo in Open/Save dialogs
            "ToolbarWindow32",   // toolbar in file dialog
            "SHELLDLL_DefView",  // shell folder view (older dialogs)
            "DirectUIHWND",      // modern shell UI host
            "ShellTabWindowClass" // tabbed shell view
        };

        public DialogSuppressor(string screenshotDir = null)
        {
            _currentProcessId = Process.GetCurrentProcess().Id;
            _screenshotDir = screenshotDir;
            _timer = new Timer { Interval = 500 };
            _timer.Tick += OnTick;
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        private void OnTick(object sender, EventArgs e)
        {
            EnumWindows(EnumCallback, IntPtr.Zero);
        }

        private bool EnumCallback(IntPtr hWnd, IntPtr lParam)
        {
            // Only inspect #32770 dialog windows
            var className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            if (className.ToString() != "#32770")
                return true;

            // Must belong to this process
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid != _currentProcessId)
                return true;

            // Must be visible
            if (!IsWindowVisible(hWnd))
                return true;

            // Already handled this window
            if (_dismissed.Contains(hWnd))
                return true;

            // Skip file dialogs — they have explorer shell child controls
            if (IsFileDialog(hWnd))
                return true;

            // Read the dialog title and body text
            var title = GetWindowTextString(hWnd);
            var body = GetDialogStaticText(hWnd);

            // Capture screenshot BEFORE dismissing so the dialog is visible in the image
            var screenshotPath = CaptureScreenshot(title);

            Console.Error.WriteLine($"[DialogSuppressor] Auto-dismissing modal dialog:");
            Console.Error.WriteLine($"  Title: {title}");
            Console.Error.WriteLine($"  Body: {body}");
            if (screenshotPath != null)
                Console.Error.WriteLine($"  Screenshot: {screenshotPath}");

            _dismissed.Add(hWnd);

            // Try WM_CLOSE first (works for OK and OK/Cancel dialogs).
            // For YesNo/RetryCancel dialogs that lack a close button, fall back
            // to clicking the first Button child control.
            PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

            // Also click the first button as fallback — handles dialogs where
            // WM_CLOSE is ignored (YesNo, RetryCancel, AbortRetryIgnore).
            ClickFirstButton(hWnd);

            return true;
        }

        private static bool IsFileDialog(IntPtr hWnd)
        {
            bool isFileDialog = false;
            EnumChildWindows(hWnd, (child, _) =>
            {
                var childClass = new StringBuilder(256);
                GetClassName(child, childClass, childClass.Capacity);
                var cls = childClass.ToString();
                foreach (var fdClass in FileDialogChildClasses)
                {
                    if (cls == fdClass)
                    {
                        isFileDialog = true;
                        return false; // stop enumeration
                    }
                }
                return true;
            }, IntPtr.Zero);
            return isFileDialog;
        }

        private static void ClickFirstButton(IntPtr hWnd)
        {
            EnumChildWindows(hWnd, (child, _) =>
            {
                var childClass = new StringBuilder(256);
                GetClassName(child, childClass, childClass.Capacity);
                if (childClass.ToString() == "Button")
                {
                    // BM_CLICK simulates a button press
                    PostMessage(child, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    return false; // stop after first button
                }
                return true;
            }, IntPtr.Zero);
        }

        /// <summary>
        /// Captures a screenshot of the primary screen (so the dialog + harness are both visible).
        /// Returns the file path, or null if screenshots are not configured.
        /// </summary>
        private string CaptureScreenshot(string dialogTitle)
        {
            if (string.IsNullOrEmpty(_screenshotDir)) return null;
            try
            {
                Directory.CreateDirectory(_screenshotDir);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var safeTitle = SanitizeFilename(dialogTitle);
                var filename = $"dialog_{safeTitle}_{timestamp}.png";
                var path = Path.Combine(_screenshotDir, filename);

                var bounds = Screen.PrimaryScreen.Bounds;
                using (var bmp = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (var g = Graphics.FromImage(bmp))
                        g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                    bmp.Save(path, ImageFormat.Png);
                }
                return path;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DialogSuppressor] Screenshot failed: {ex.Message}");
                return null;
            }
        }

        private static string SanitizeFilename(string name)
        {
            if (string.IsNullOrEmpty(name)) return "untitled";
            var sb = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                    sb.Append(c);
                else if (c == ' ')
                    sb.Append('_');
            }
            var result = sb.ToString();
            return result.Length > 40 ? result.Substring(0, 40) : result;
        }

        private static string GetWindowTextString(IntPtr hWnd)
        {
            var sb = new StringBuilder(1024);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static string GetDialogStaticText(IntPtr hWnd)
        {
            var result = new StringBuilder();
            EnumChildWindows(hWnd, (child, _) =>
            {
                var childClass = new StringBuilder(256);
                GetClassName(child, childClass, childClass.Capacity);
                if (childClass.ToString() == "Static")
                {
                    var text = new StringBuilder(4096);
                    GetWindowText(child, text, text.Capacity);
                    if (text.Length > 0)
                    {
                        if (result.Length > 0) result.Append(" | ");
                        result.Append(text);
                    }
                }
                return true;
            }, IntPtr.Zero);
            return result.ToString();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _timer.Dispose();
        }

        // Win32 constants
        private const uint WM_CLOSE = 0x0010;
        private const uint BM_CLICK = 0x00F5;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxLength);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxLength);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
    }
}
