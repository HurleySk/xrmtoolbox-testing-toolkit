using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace XrmToolBox.TestHarness
{
    public static class PluginLoader
    {
        public static PluginControlBase LoadPlugin(string dllPath)
        {
            var fullPath = Path.GetFullPath(dllPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Plugin DLL not found: {fullPath}");

            var pluginDir = Path.GetDirectoryName(fullPath);

            // Register assembly resolver for plugin dependencies
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var assemblyName = new AssemblyName(args.Name).Name + ".dll";
                var candidatePath = Path.Combine(pluginDir, assemblyName);
                if (File.Exists(candidatePath))
                    return Assembly.LoadFrom(candidatePath);
                return null;
            };

            // Try MEF first (how XrmToolBox discovers plugins)
            try
            {
                var assembly = Assembly.LoadFrom(fullPath);
                var catalog = new AssemblyCatalog(assembly);
                var container = new CompositionContainer(catalog);
                var plugin = container.GetExportedValueOrDefault<IXrmToolBoxPlugin>();

                if (plugin != null)
                {
                    var control = plugin.GetControl();
                    if (control is PluginControlBase pcb)
                        return pcb;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MEF discovery failed, falling back to reflection: {ex.Message}");
            }

            // Fallback: scan for PluginControlBase subclasses
            return LoadViaReflection(fullPath);
        }

        private static PluginControlBase LoadViaReflection(string dllPath)
        {
            var assembly = Assembly.LoadFrom(dllPath);

            var controlType = assembly.GetExportedTypes()
                .FirstOrDefault(t => typeof(PluginControlBase).IsAssignableFrom(t)
                                     && !t.IsAbstract
                                     && t.GetConstructor(Type.EmptyTypes) != null);

            if (controlType == null)
                throw new InvalidOperationException(
                    $"No PluginControlBase subclass with a parameterless constructor found in {Path.GetFileName(dllPath)}");

            return (PluginControlBase)Activator.CreateInstance(controlType);
        }

        public static string GetPluginDisplayName(string dllPath)
        {
            try
            {
                var assembly = Assembly.LoadFrom(Path.GetFullPath(dllPath));
                var pluginType = assembly.GetExportedTypes()
                    .FirstOrDefault(t => typeof(IXrmToolBoxPlugin).IsAssignableFrom(t) && !t.IsAbstract);

                if (pluginType != null)
                {
                    var attr = pluginType.GetCustomAttributes(typeof(ExportMetadataAttribute), false)
                        .Cast<ExportMetadataAttribute>()
                        .FirstOrDefault(a => a.Name == "Name");
                    if (attr != null)
                        return attr.Value?.ToString();
                }
            }
            catch
            {
                // Ignore — fall back to filename
            }

            return Path.GetFileNameWithoutExtension(dllPath);
        }
    }
}
