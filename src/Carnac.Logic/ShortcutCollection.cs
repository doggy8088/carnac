using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Carnac.Logic.Models;

namespace Carnac.Logic
{
    public class ShortcutCollection : Collection<KeyShortcut>
    {
        static readonly string[] AllProcesses = new string[0];

        string process;
        string[] processNames = AllProcesses;

        public string Group { get; set; }

        /// <summary>
        /// The processes this keymap applies to: one process name as Task Manager shows it without ".exe" ("Code"),
        /// or several names separated by '|' ("chrome|msedge"). Names are compared case-insensitively and in full
        /// ("chrome" does not match "chromedriver"). Empty means every process.
        /// </summary>
        public string Process
        {
            get { return process; }
            set
            {
                process = value;
                processNames = ParseProcessNames(value);
            }
        }

        /// <summary>The individual process names of <see cref="Process"/>; empty when the keymap applies to every process.</summary>
        public IReadOnlyList<string> ProcessNames
        {
            get { return processNames; }
        }

        public ShortcutCollection(IList<KeyShortcut> values)
            : base(values)
        {

        }

        /// <summary>True when the keymap applies to keys pressed in the given process (<see cref="System.Diagnostics.Process.ProcessName"/>).</summary>
        public bool AppliesTo(string processName)
        {
            if (processNames.Length == 0)
                return true;

            return processName != null
                && processNames.Any(name => string.Equals(name, processName, StringComparison.OrdinalIgnoreCase));
        }

        public KeyShortcut[] GetShortcutsMatching(IEnumerable<KeyPress> keys)
        {
            return this.Where(s => s.StartsWith(keys)).ToArray();
        }

        static string[] ParseProcessNames(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return AllProcesses;

            return value.Split('|')
                .Select(name => name.Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
