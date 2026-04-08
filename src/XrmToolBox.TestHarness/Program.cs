using System;
using System.Drawing;
using System.Windows.Forms;
using McTools.Xrm.Connection;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using XrmToolBox.TestHarness.MockService;

namespace XrmToolBox.TestHarness
{
    static class Program
    {
        private static void FlushRecorder(RequestRecorder recorder, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                recorder.SaveToFile(path);
                Console.WriteLine($"SDK call recording saved: {path}");
                Console.WriteLine($"Total calls recorded: {recorder.Calls.Count}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error saving recording: {ex.Message}");
            }
        }

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

            var recorder = new RequestRecorder();
            IOrganizationService service;
            ConnectionDetail connectionDetail;
            IDisposable serviceToDispose = null;

            if (!string.IsNullOrEmpty(options.ConnectionString))
            {
                // Real Dataverse connection
                ServiceClient serviceClient;
                try
                {
                    serviceClient = new ServiceClient(options.ConnectionString);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error creating service client: {ex.Message}");
                    return;
                }

                if (!serviceClient.IsReady)
                {
                    Console.Error.WriteLine($"Failed to connect: {serviceClient.LastError}");
                    serviceClient.Dispose();
                    return;
                }

                Console.WriteLine($"Connected to: {serviceClient.ConnectedOrgFriendlyName}");
                serviceToDispose = serviceClient;

                service = !string.IsNullOrEmpty(options.RecordingOutputPath)
                    ? new RecordingServiceDecorator(serviceClient, recorder)
                    : (IOrganizationService)serviceClient;

                connectionDetail = new ConnectionDetail
                {
                    OrganizationFriendlyName = serviceClient.ConnectedOrgFriendlyName,
                    WebApplicationUrl = serviceClient.ConnectedOrgUriActual?.ToString(),
                    OrganizationVersion = serviceClient.ConnectedOrgVersion?.ToString() ?? "9.2.0.0",
                    ServerName = serviceClient.ConnectedOrgUriActual?.Host ?? "unknown"
                };
            }
            else
            {
                // Mock service path
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

                service = new MockOrganizationService(dataStore, recorder);

                connectionDetail = new ConnectionDetail
                {
                    OrganizationFriendlyName = options.OrgName,
                    WebApplicationUrl = "https://mock.crm.dynamics.com",
                    OrganizationVersion = "9.2.0.0",
                    ServerName = "mock.crm.dynamics.com"
                };
            }

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
                serviceToDispose?.Dispose();
                return;
            }

            var form = new HarnessForm(
                pluginControl,
                service,
                connectionDetail,
                new Size(options.Width, options.Height),
                options.AutoConnect,
                options.ScreenshotDir);

            form.Text = $"Test Harness - {pluginName}";

            // Start periodic auto-flush of call recordings so data survives crashes
            if (!string.IsNullOrEmpty(options.RecordingOutputPath))
                recorder.StartAutoFlush(options.RecordingOutputPath);

            // Catch unhandled exceptions so we can flush the recorder before dying
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                Console.Error.WriteLine($"Unhandled UI thread exception: {e.Exception}");
                FlushRecorder(recorder, options.RecordingOutputPath);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Console.Error.WriteLine($"Unhandled exception: {e.ExceptionObject}");
                FlushRecorder(recorder, options.RecordingOutputPath);
            };

            Application.Run(form);

            // Final flush on clean exit
            FlushRecorder(recorder, options.RecordingOutputPath);
            recorder.Dispose();
            serviceToDispose?.Dispose();
        }
    }
}
