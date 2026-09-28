using System.Collections.Generic;
using System.Linq;
using SettingsProviderNet;

namespace Carnac.Tests
{
    /// <summary>
    /// Keeps the settings in memory so tests can use a real <see cref="SettingsProvider"/> (with its caching and defaults).
    /// </summary>
    public class InMemorySettingsStorage : ISettingsStorage
    {
        readonly Dictionary<string, Dictionary<string, string>> stored = new Dictionary<string, Dictionary<string, string>>();

        public void Save(string key, Dictionary<string, string> settings)
        {
            stored[key] = new Dictionary<string, string>(settings);
        }

        public Dictionary<string, string> Load(string key)
        {
            Dictionary<string, string> settings;
            return stored.TryGetValue(key, out settings)
                ? new Dictionary<string, string>(settings)
                : new Dictionary<string, string>();
        }

        /// <summary>Removes one setting from everything that was saved, like a settings file written by an older version.</summary>
        public void Remove(string settingKey)
        {
            foreach (var settings in stored.Values.ToList())
            {
                settings.Remove(settingKey);
            }
        }
    }
}
