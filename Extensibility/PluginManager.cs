using System;
using System.Collections.Generic;
using System.Linq;

namespace programmersdigest.Extensibility {
    public class PluginManager {
        public delegate object CreatePluginInstanceCallback(Type implementation);

        private const string PluginsAppDomainName = "PluginsDomain";

        private string _searchPattern;
        private Dictionary<string, List<string>> _pluginsCache;
        private CreatePluginInstanceCallback _createPluginInstanceCallback;

        public PluginManager() : this(@"*.plugin.dll") {
        }

        public PluginManager(string searchPattern) {
            _searchPattern = searchPattern;
            _createPluginInstanceCallback = DefaultCreatePluginInstanceCallback;
        }

        public void RegisterCreatePluginInstanceCallback(CreatePluginInstanceCallback callback) {
            _createPluginInstanceCallback = callback;
        }

        public void DiscoverPlugins() {
            var loader = new PluginLoader();
            _pluginsCache = loader.DiscoverPlugins(_searchPattern);
        }

        public IEnumerable<Type> Find<TContract>() {
            return Find(typeof(TContract));
        }

        public IEnumerable<Type> Find(Type contract) {
            if (_pluginsCache == null) {
                throw new InvalidOperationException($"The plugins cache is invalid. Please execute {nameof(DiscoverPlugins)} prior to retrieving plugins.");
            }

            if (contract == null) {
                throw new ArgumentNullException(nameof(contract), "Contract must not be null");
            }

            lock (_pluginsCache) {
                if (!_pluginsCache.TryGetValue(contract.AssemblyQualifiedName, out var implementations)) {
                    implementations = new List<string>();
                }

                return implementations.Select(i => Type.GetType(i));
            }
        }

        public IEnumerable<TContract> Load<TContract>() {
            var implementations = Find<TContract>();
            foreach (var implementation in implementations) {
                yield return (TContract)_createPluginInstanceCallback(implementation);
            }
        }

        private object DefaultCreatePluginInstanceCallback(Type implementation) {
            return Activator.CreateInstance(implementation);
        }
    }
}
