using System.Collections.Generic;
using SettingsProviderNet;

namespace Carnac.Tests
{
    /// <summary>Keeps the settings in memory, so tests can use the real SettingsProvider without touching %APPDATA%.</summary>
    public class InMemoryStorage : ISettingsStorage
    {
        readonly Dictionary<string, Dictionary<string, string>> stored = new Dictionary<string, Dictionary<string, string>>();

        public void Save(string name, Dictionary<string, string> values)
        {
            stored[name] = new Dictionary<string, string>(values);
        }

        public Dictionary<string, string> Load(string name)
        {
            Dictionary<string, string> values;
            return stored.TryGetValue(name, out values) ? new Dictionary<string, string>(values) : new Dictionary<string, string>();
        }

        /// <summary>Simulates a settings file that was written by an older Carnac version.</summary>
        public void Seed(string name, Dictionary<string, string> values)
        {
            stored[name] = values;
        }
    }
}
