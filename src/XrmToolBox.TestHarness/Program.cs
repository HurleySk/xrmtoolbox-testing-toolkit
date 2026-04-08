using System;
using System.Drawing;
using System.Windows.Forms;
using McTools.Xrm.Connection;
using XrmToolBox.TestHarness.MockService;

namespace XrmToolBox.TestHarness
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            CommandLineOptions options;
            try
            {
                options = CommandLineOptions.Parse(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error parsing arguments: {ex.Message}");
                return;
            }

            // Load mock data
            MockDataStore dataStore;
            try
            {
                dataStore = new MockDataStore(options.MockDataPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading mock data: {ex.Message}");
                return;
            }

            var recorder = new RequestRecorder();
            var mockService = new MockOrganizationService(dataStore, recorder);

            // Create stub ConnectionDetail
            var connectionDetail = new ConnectionDetail
            {
                OrganizationFriendlyName = options.OrgName,
                WebApplicationUrl = "https://mock.crm.dynamics.com",
                OrganizationVersion = "9.2.0.0",
                ServerName = "mock.crm.dynamics.com"
            };

            // Load plugin
            Console.WriteLine($"Loading plugin from: {options.PluginDllPath}");
            var pluginName = PluginLoader.GetPluginDisplayName(options.PluginDllPath);

            XrmToolBox.Extensibility.PluginControlBase pluginControl;
            try
            {
                pluginControl = PluginLoader.LoadPlugin(options.PluginDllPath);
                Console.WriteLine($"Plugin loaded: {pluginName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading plugin: {ex.Message}");
                if (ex.InnerException != null)
                    Console.Error.WriteLine($"  Inner: {ex.InnerException.Message}");
                return;
            }

            // Update connection detail with plugin name
            connectionDetail.OrganizationFriendlyName = options.OrgName;

            var form = new HarnessForm(
                pluginControl,
                mockService,
                connectionDetail,
                new Size(options.Width, options.Height),
                options.AutoConnect,
                options.ScreenshotDir);

            form.Text = $"Test Harness - {pluginName}";

            Application.Run(form);

            // On exit, write recorded calls
            if (!string.IsNullOrEmpty(options.RecordingOutputPath))
            {
                try
                {
                    recorder.SaveToFile(options.RecordingOutputPath);
                    Console.WriteLine($"SDK call recording saved: {options.RecordingOutputPath}");
                    Console.WriteLine($"Total calls recorded: {recorder.Calls.Count}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error saving recording: {ex.Message}");
                }
            }
        }
    }
}
