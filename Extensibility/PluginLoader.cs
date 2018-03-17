using programmersdigest.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace programmersdigest.Extensibility {
    internal class PluginLoader {
        private const string PluginsAppDomainName = "PluginsDomain";

        public Dictionary<string, List<string>> DiscoverPlugins(string searchPattern) {
            // TODO NetCore does NOT implement AppDomains. Instead we are supposed to use AssemblyLoadContext, which
            // in turn IS NOT PART OF netstandard 2.0 and NOT IMPLEMENTED in .NET 4.6.2.
            // WTF???

            AppDomain appDomain = null;
            try {
                appDomain = AppDomain.CreateDomain(PluginsAppDomainName);
                var exeDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                var pluginAssemblyFiles = Directory.EnumerateFiles(exeDir, searchPattern, SearchOption.AllDirectories);

                return LoadPluginAssemblies(appDomain, pluginAssemblyFiles);
            }
            finally {
                if (appDomain != null) {
                    AppDomain.Unload(appDomain);
                }
            }
        }

        private Dictionary<string, List<string>> LoadPluginAssemblies(AppDomain appDomain, IEnumerable<string> pluginAssemblyFiles) {
            var cache = new Dictionary<string, List<string>>();
            var exceptions = new List<Exception>();

            foreach (var pluginAssemblyFile in pluginAssemblyFiles) {
                try {
                    // Append private path to temporary and main AppDomain.
                    // Yes, AppendPrivatePath is obsolete, but netstandard 2.0
                    // DOES NOT PROVIDE AN ALTERNATIVE! Thanks a bunch!
#pragma warning disable CS0618
                    appDomain.AppendPrivatePath(Path.GetDirectoryName(pluginAssemblyFile));
                    AppDomain.CurrentDomain.AppendPrivatePath(Path.GetDirectoryName(pluginAssemblyFile));
#pragma warning restore CS0618

                    var name = AssemblyName.GetAssemblyName(pluginAssemblyFile);
                    var assembly = appDomain.Load(name);

                    DiscoverPluginTypes(assembly, ref cache);
                }
                catch (Exception ex) {
                    exceptions.Add(ex);
                }
            }

            if (exceptions.Any()) {
                throw new AggregateException("One or more plugins could not be loaded.", exceptions);
            }

            return cache;
        }

        private void DiscoverPluginTypes(Assembly assembly, ref Dictionary<string, List<string>> cache) {
            foreach (var type in assembly.GetTypes()) {
                if (type.IsInterface || type.IsAbstract) {
                    continue;
                }

                var pluginInterfaces = type.GetInterfaces();        // TODO This could become a rather huge interface map, no? Is this a problem?
                foreach (var pluginInterface in pluginInterfaces) {
                    cache.Add(pluginInterface.AssemblyQualifiedName, type.AssemblyQualifiedName);
                }
            }
        }
    }
}
