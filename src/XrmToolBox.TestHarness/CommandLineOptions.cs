using System;
using System.Collections.Generic;

namespace XrmToolBox.TestHarness
{
    public class CommandLineOptions
    {
        public string PluginDllPath { get; set; }
        public string MockDataPath { get; set; }
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 768;
        public string ScreenshotDir { get; set; }
        public bool AutoConnect { get; set; } = true;
        public string OrgName { get; set; } = "Mock Organization";
        public string RecordingOutputPath { get; set; }

        public static CommandLineOptions Parse(string[] args)
        {
            var options = new CommandLineOptions();
            var queue = new Queue<string>(args);

            while (queue.Count > 0)
            {
                var arg = queue.Dequeue();
                switch (arg.ToLowerInvariant())
                {
                    case "--plugin":
                    case "-p":
                        options.PluginDllPath = Dequeue(queue, arg);
                        break;
                    case "--mockdata":
                    case "-m":
                        options.MockDataPath = Dequeue(queue, arg);
                        break;
                    case "--width":
                        options.Width = int.Parse(Dequeue(queue, arg));
                        break;
                    case "--height":
                        options.Height = int.Parse(Dequeue(queue, arg));
                        break;
                    case "--screenshots":
                    case "-s":
                        options.ScreenshotDir = Dequeue(queue, arg);
                        break;
                    case "--org":
                        options.OrgName = Dequeue(queue, arg);
                        break;
                    case "--record":
                    case "-r":
                        options.RecordingOutputPath = Dequeue(queue, arg);
                        break;
                    case "--no-autoconnect":
                        options.AutoConnect = false;
                        break;
                    case "--help":
                    case "-h":
                        PrintUsage();
                        Environment.Exit(0);
                        break;
                    default:
                        if (!arg.StartsWith("-") && string.IsNullOrEmpty(options.PluginDllPath))
                            options.PluginDllPath = arg;
                        break;
                }
            }

            if (string.IsNullOrEmpty(options.PluginDllPath))
            {
                Console.Error.WriteLine("Error: --plugin <path> is required.");
                Console.Error.WriteLine();
                PrintUsage();
                Environment.Exit(1);
            }

            return options;
        }

        private static string Dequeue(Queue<string> queue, string flag)
        {
            if (queue.Count == 0)
                throw new ArgumentException($"Missing value for {flag}");
            return queue.Dequeue();
        }

        private static void PrintUsage()
        {
            Console.WriteLine("XrmToolBox Test Harness");
            Console.WriteLine();
            Console.WriteLine("Usage: XrmToolBox.TestHarness.exe --plugin <dll> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --plugin, -p <path>      Path to the XrmToolBox plugin DLL (required)");
            Console.WriteLine("  --mockdata, -m <path>    Path to JSON mock data configuration");
            Console.WriteLine("  --width <pixels>         Window width (default: 1024)");
            Console.WriteLine("  --height <pixels>        Window height (default: 768)");
            Console.WriteLine("  --screenshots, -s <dir>  Directory for screenshot output");
            Console.WriteLine("  --org <name>             Organization display name (default: Mock Organization)");
            Console.WriteLine("  --record, -r <path>      Record SDK calls to JSON file on exit");
            Console.WriteLine("  --no-autoconnect         Don't inject mock service on load");
            Console.WriteLine("  --help, -h               Show this help");
        }
    }
}
