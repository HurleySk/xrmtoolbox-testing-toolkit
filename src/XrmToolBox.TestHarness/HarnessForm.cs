using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using XrmToolBox.Extensibility;

namespace XrmToolBox.TestHarness
{
    public class HarnessForm : Form
    {
        private readonly PluginControlBase _pluginControl;
        private readonly IOrganizationService _service;
        private readonly ConnectionDetail _connectionDetail;
        private readonly bool _autoConnect;
        private readonly string _screenshotDir;
        private int _screenshotCount;

        public HarnessForm(PluginControlBase pluginControl, IOrganizationService service,
            ConnectionDetail connectionDetail, Size windowSize, bool autoConnect,
            string screenshotDir)
        {
            _pluginControl = pluginControl;
            _service = service;
            _connectionDetail = connectionDetail;
            _autoConnect = autoConnect;
            _screenshotDir = screenshotDir;

            Text = $"Test Harness - {connectionDetail.OrganizationFriendlyName}";
            Size = windowSize;
            StartPosition = FormStartPosition.CenterScreen;
            AccessibleName = "XrmToolBoxTestHarness";
            Name = "HarnessForm";
            KeyPreview = true;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            SetupNotificationArea();
            HostPlugin();

            if (_autoConnect)
                InjectService();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.F12)
            {
                TakeScreenshot("manual");
                e.Handled = true;
            }
        }

        private void SetupNotificationArea()
        {
            // PluginControlBase.ShowInfoNotification() searches for
            // Parent.Controls.Find("NotifPanel", false). We create a minimal
            // panel to satisfy this without crashing.
            var notifPanel = new Panel
            {
                Name = "NotifPanel",
                Dock = DockStyle.Top,
                Height = 0,
                Visible = false
            };
            Controls.Add(notifPanel);
        }

        private void HostPlugin()
        {
            _pluginControl.Dock = DockStyle.Fill;
            Controls.Add(_pluginControl);
            _pluginControl.BringToFront();
        }

        private void InjectService()
        {
            // Wire the OnRequestConnection event first — this fires when the
            // plugin calls ExecuteMethod() and Service is null.
            _pluginControl.OnRequestConnection += OnPluginRequestConnection;

            // Inject service via UpdateConnection (same mechanism XrmToolBox uses).
            // Empty actionName avoids triggering deferred method invocation.
            _pluginControl.UpdateConnection(_service, _connectionDetail, string.Empty, null);
        }

        private void OnPluginRequestConnection(object sender, EventArgs e)
        {
            // When a plugin calls ExecuteMethod(SomeAction) and isn't connected,
            // it fires OnRequestConnection. We inject the service and the deferred
            // action executes via reflection.
            if (e is RequestConnectionEventArgs reqArgs)
            {
                _pluginControl.UpdateConnection(
                    _service,
                    _connectionDetail,
                    reqArgs.ActionName,
                    reqArgs.Parameter);
            }
            else
            {
                _pluginControl.UpdateConnection(
                    _service,
                    _connectionDetail,
                    string.Empty,
                    null);
            }
        }

        public string TakeScreenshot(string prefix = "screenshot")
        {
            if (string.IsNullOrEmpty(_screenshotDir))
                return null;

            Directory.CreateDirectory(_screenshotDir);

            _screenshotCount++;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var filename = $"{prefix}_{timestamp}.png";
            var path = Path.Combine(_screenshotDir, filename);

            using (var bmp = new Bitmap(Width, Height))
            {
                DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }

            Console.WriteLine($"Screenshot saved: {path}");
            return path;
        }
    }
}
