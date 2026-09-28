using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Carnac.Logic.Models;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Carnac.Logic
{
    public class ShortcutProvider : IShortcutProvider
    {
        readonly List<ShortcutCollection> shortcuts;
        readonly Action<string> warn;
        readonly KeyCombinationParser parser;

        /// <summary>Loads the keymaps installed next to the running executable (the "Keymaps" folder).</summary>
        public ShortcutProvider()
            : this(GetDefaultKeymapFolder())
        {
        }

        /// <param name="keymapFolder">Folder containing the *.yml keymap files; a missing folder means no keymaps.</param>
        /// <param name="warn">
        /// Receives one message for every keymap file or entry that is ignored because it cannot be understood.
        /// Null writes the messages to <see cref="System.Diagnostics.Trace"/>.
        /// </param>
        public ShortcutProvider(string keymapFolder, Action<string> warn = null)
        {
            if (keymapFolder == null)
                throw new ArgumentNullException("keymapFolder");

            this.warn = warn ?? KeyCombinationParser.TraceWarning;
            parser = new KeyCombinationParser(this.warn);

            shortcuts = Directory.Exists(keymapFolder)
                ? Directory.GetFiles(keymapFolder, "*.yml")
                    .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                    .Select(LoadShortcuts)
                    .Where(collection => collection != null)
                    .ToList()
                : new List<ShortcutCollection>();
        }

        /// <summary>The keymaps that were loaded, one collection per keymap file.</summary>
        public IReadOnlyList<ShortcutCollection> Keymaps
        {
            get { return shortcuts; }
        }

        public List<KeyShortcut> GetShortcutsStartingWith(KeyPress keys)
        {
            var processName = keys.Process.ProcessName;
            return shortcuts
                .Where(s => s.Process == processName || string.IsNullOrWhiteSpace(s.Process))
                .SelectMany(shortcut => shortcut.GetShortcutsMatching(new[] { keys }))
                .ToList();
        }

        static string GetDefaultKeymapFolder()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return Path.GetDirectoryName(process.MainModule.FileName) + @"\Keymaps\";
            }
        }

        ShortcutCollection LoadShortcuts(string file)
        {
            var fileName = Path.GetFileName(file);
            YamlMappingNode root;
            try
            {
                var yaml = new YamlStream();
                using (var reader = File.OpenText(file))
                {
                    yaml.Load(reader);
                }
                root = yaml.Documents.Count > 0 ? yaml.Documents[0].RootNode as YamlMappingNode : null;
            }
            catch (YamlException exception)
            {
                warn(fileName + ": ignoring keymap, it is not valid YAML: " + exception.Message);
                return null;
            }
            catch (IOException exception)
            {
                warn(fileName + ": ignoring keymap, it cannot be read: " + exception.Message);
                return null;
            }
            catch (UnauthorizedAccessException exception)
            {
                warn(fileName + ": ignoring keymap, it cannot be read: " + exception.Message);
                return null;
            }

            if (root == null)
            {
                warn(fileName + ": ignoring keymap, the top level must be a mapping with 'group', 'process' and 'shortcuts'");
                return null;
            }

            return GetShortcuts(root, fileName);
        }

        static string GetValueByKey(YamlMappingNode node, string name)
        {
            var child = node.Children.FirstOrDefault(n => n.Key.ToString() == name);
            return child.Value == null ? null : child.Value.ToString();
        }

        ShortcutCollection GetShortcuts(YamlMappingNode collection, string fileName)
        {
            string group = GetValueByKey(collection, "group");
            string process = GetValueByKey(collection, "process");

            var shortCuts = from groupShortcuts in collection.Children.Where(n => n.Key.ToString() == "shortcuts").Take(1).Select(x => x.Value).OfType<YamlSequenceNode>()
                            from shortcut in GetKeyShortcuts(groupShortcuts, fileName)
                            select shortcut;

            return new ShortcutCollection(shortCuts.ToList())
            {
                Process = process,
                Group = @group
            };
        }

        IEnumerable<KeyShortcut> GetKeyShortcuts(YamlSequenceNode groupShortcuts, string fileName)
        {
            foreach (var entry in groupShortcuts.Children.OfType<YamlMappingNode>())
            {
                var name = GetValueByKey(entry, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    warn(fileName + ": ignoring a shortcut without a 'name'");
                    continue;
                }

                var context = fileName + ", shortcut '" + name + "'";
                var keys = entry.Children.Where(n => n.Key.ToString() == "keys").Take(1).Select(x => x.Value).OfType<YamlSequenceNode>().FirstOrDefault();
                if (keys == null)
                {
                    warn(context + ": ignoring shortcut, it has no 'keys' list");
                    continue;
                }

                foreach (var keyCombo in keys.Children)
                {
                    // Every element of "keys" is one alternative way to trigger the shortcut; commas inside it
                    // separate the key presses of a chord. An element that cannot be understood is dropped as a whole,
                    // never half-read (a chord cut short would match the wrong keys).
                    var definitions = parser.ParseSequence(keyCombo.ToString(), context);
                    if (definitions != null && definitions.Count > 0)
                        yield return new KeyShortcut(name, definitions.ToArray());
                }
            }
        }
    }
}
