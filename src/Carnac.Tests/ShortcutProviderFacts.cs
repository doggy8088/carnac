using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Xunit;

namespace Carnac.Tests
{
    public class ShortcutProviderFacts : IDisposable
    {
        readonly List<string> warnings = new List<string>();
        readonly List<string> temporaryFolders = new List<string>();

        public void Dispose()
        {
            foreach (var folder in temporaryFolders)
            {
                try
                {
                    Directory.Delete(folder, true);
                }
                catch (IOException)
                {
                    // best effort: a leftover folder below the temp directory is harmless
                }
            }
        }

        /// <summary>
        /// The keymaps exactly as they are checked in. The repository can be checked out anywhere (developer machine,
        /// Cake/AppVeyor build in-tree), so walk up from the test assembly until src\Carnac.Logic\Keymaps is found.
        /// </summary>
        static string FindShippedKeymapsFolder()
        {
            var assemblyFolder = Path.GetDirectoryName(new Uri(typeof(ShortcutProviderFacts).Assembly.CodeBase).LocalPath);
            for (var folder = new DirectoryInfo(assemblyFolder); folder != null; folder = folder.Parent)
            {
                var candidate = Path.Combine(folder.FullName, "src", "Carnac.Logic", "Keymaps");
                if (Directory.Exists(candidate))
                    return candidate;
            }

            // Test binaries copied away from the source tree still carry the keymaps the build copied next to them.
            var copiedNextToTests = Path.Combine(assemblyFolder, "Keymaps");
            Assert.True(Directory.Exists(copiedNextToTests),
                "Could not find src\\Carnac.Logic\\Keymaps above " + assemblyFolder + " or a Keymaps folder next to the test assembly");
            return copiedNextToTests;
        }

        string CreateKeymapFolder(params string[] fileNameAndContent)
        {
            var folder = Path.Combine(Path.GetTempPath(), "carnac-keymaps-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            temporaryFolders.Add(folder);
            for (var i = 0; i < fileNameAndContent.Length; i += 2)
                File.WriteAllText(Path.Combine(folder, fileNameAndContent[i]), fileNameAndContent[i + 1]);
            return folder;
        }

        static KeyPress Press(string processName, Keys key, bool control = false, bool shift = false, bool alt = false)
        {
            var args = new InterceptKeyEventArgs(key, KeyDirection.Down, alt, control, shift);
            return new KeyPress(new ProcessInfo(processName), args, false, new[] { key.ToString() });
        }

        // Every entry of every shipped keymap has to be understood by the parser. A bad entry used to be misread or
        // dropped without a trace (Enter labelled "Open the Chrome menu", vscode chords cut to their last key, ...).
        // There is no allow-list: an entry that names a key the parser cannot resolve must be fixed in the keymap.
        [Fact]
        public void every_entry_of_every_shipped_keymap_parses()
        {
            var folder = FindShippedKeymapsFolder();
            var files = Directory.GetFiles(folder, "*.yml");
            Assert.True(files.Length >= 6, "Expected the shipped keymaps in " + folder + " but found " + files.Length + " files");

            var provider = new ShortcutProvider(folder, warnings.Add);

            Assert.True(warnings.Count == 0,
                "Keymap entries that cannot be parsed:" + Environment.NewLine + string.Join(Environment.NewLine, warnings));
            Assert.Equal(files.Length, provider.Keymaps.Count);
            foreach (var keymap in provider.Keymaps)
                Assert.True(keymap.Count > 0, "Keymap '" + keymap.Group + "' has no shortcuts");
        }

        [Fact]
        public void plain_enter_in_chrome_is_not_a_shortcut()
        {
            // "F10+Enter" in chrome.yml used to be read as a bare Enter.
            var provider = new ShortcutProvider(FindShippedKeymapsFolder(), warnings.Add);

            Assert.Empty(provider.GetShortcutsStartingWith(Press("chrome", Keys.Enter)));
        }

        [Fact]
        public void chrome_shortcut_with_modifiers_is_still_recognised()
        {
            var provider = new ShortcutProvider(FindShippedKeymapsFolder(), warnings.Add);

            var shortcuts = provider.GetShortcutsStartingWith(Press("chrome", Keys.N, control: true));

            Assert.Equal("Open a new window", shortcuts.Single().Name);
        }

        [Fact]
        public void vscode_chords_keep_every_key_press()
        {
            var provider = new ShortcutProvider(FindShippedKeymapsFolder(), warnings.Add);

            // "Ctrl+K Ctrl+D" used to be read as a plain Ctrl+D, which is a different shortcut.
            var startingWithCtrlD = provider.GetShortcutsStartingWith(Press("Code", Keys.D, control: true));
            Assert.Equal("Add Selection To Next Find Match", startingWithCtrlD.Single().Name);

            var startingWithCtrlK = provider.GetShortcutsStartingWith(Press("Code", Keys.K, control: true));
            var chord = startingWithCtrlK.Single(shortcut => shortcut.Name == "Move Last Selection To Next Find Match");
            Assert.True(chord.StartsWith(new[] { new KeyPressDefinition(Keys.K, controlPressed: true) }));
            Assert.True(chord.IsMatch(new[]
            {
                Press("Code", Keys.K, control: true),
                Press("Code", Keys.D, control: true)
            }));
        }

        [Fact]
        public void number_keys_in_keymaps_match_the_digit_keys()
        {
            // "Ctrl+1" used to resolve to key code 1 (the left mouse button) so it never matched.
            var provider = new ShortcutProvider(FindShippedKeymapsFolder(), warnings.Add);

            var shortcuts = provider.GetShortcutsStartingWith(Press("chrome", Keys.D1, control: true));

            Assert.Equal("Jump to first tab", shortcuts.Single().Name);
        }

        [Fact]
        public void missing_folder_gives_no_shortcuts()
        {
            var missing = Path.Combine(Path.GetTempPath(), "carnac-no-such-folder-" + Guid.NewGuid().ToString("N"));

            var provider = new ShortcutProvider(missing, warnings.Add);

            Assert.Empty(provider.Keymaps);
            Assert.Empty(provider.GetShortcutsStartingWith(Press("chrome", Keys.N, control: true)));
            Assert.Empty(warnings);
        }

        [Fact]
        public void keymap_folder_is_required()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new ShortcutProvider(null));

            Assert.Equal("keymapFolder", exception.ParamName);
        }

        [Fact]
        public void loads_shortcuts_from_the_given_folder()
        {
            var folder = CreateKeymapFolder("test.yml",
                "group: Test\nprocess: notepad\nshortcuts:\n  - name: Save\n    keys:\n      - Ctrl+S\n      - Ctrl+K,S\n");

            var provider = new ShortcutProvider(folder, warnings.Add);

            Assert.Empty(warnings);
            var keymap = provider.Keymaps.Single();
            Assert.Equal("Test", keymap.Group);
            Assert.Equal("notepad", keymap.Process);
            Assert.Equal(2, keymap.Count);
            Assert.Equal(2, provider.GetShortcutsStartingWith(Press("notepad", Keys.S, control: true)).Count
                + provider.GetShortcutsStartingWith(Press("notepad", Keys.K, control: true)).Count);
        }

        [Fact]
        public void ignores_files_that_are_not_keymaps()
        {
            var folder = CreateKeymapFolder(
                "test.yml", "group: Test\nprocess:\nshortcuts:\n  - name: Save\n    keys:\n      - Ctrl+S\n",
                "test.yml.sample", "group: Sample\nprocess:\nshortcuts:\n  - name: Sample\n    keys:\n      - Ctrl+Q\n",
                "readme.txt", "not a keymap");

            var provider = new ShortcutProvider(folder, warnings.Add);

            Assert.Equal("Test", provider.Keymaps.Single().Group);
        }

        [Fact]
        public void unparsable_entry_is_dropped_with_a_warning_naming_file_shortcut_and_text()
        {
            var folder = CreateKeymapFolder("bad.yml",
                "group: Bad\nprocess:\nshortcuts:\n" +
                "  - name: Delete line\n    keys:\n      - Ctrl+K Ctrl+D\n" +
                "  - name: Good\n    keys:\n      - Ctrl+G\n");

            var provider = new ShortcutProvider(folder, warnings.Add);

            var warning = Assert.Single(warnings);
            Assert.Contains("bad.yml", warning);
            Assert.Contains("Delete line", warning);
            Assert.Contains("Ctrl+K Ctrl+D", warning);
            Assert.Equal("Good", provider.Keymaps.Single().Single().Name);
        }

        [Fact]
        public void chord_with_an_unusable_part_is_dropped_entirely_instead_of_cut_short()
        {
            var folder = CreateKeymapFolder("chord.yml",
                "group: Chord\nprocess:\nshortcuts:\n  - name: Broken chord\n    keys:\n      - Ctrl+K,Nonsense\n");

            var provider = new ShortcutProvider(folder, warnings.Add);

            Assert.Single(warnings);
            Assert.Empty(provider.Keymaps.Single());
            Assert.Empty(provider.GetShortcutsStartingWith(Press("anything", Keys.K, control: true)));
        }

        [Fact]
        public void shortcut_without_name_or_keys_is_reported()
        {
            var folder = CreateKeymapFolder("odd.yml",
                "group: Odd\nprocess:\nshortcuts:\n  - keys:\n      - Ctrl+S\n  - name: No keys here\n");

            var provider = new ShortcutProvider(folder, warnings.Add);

            Assert.Equal(2, warnings.Count);
            Assert.Contains("without a 'name'", warnings[0]);
            Assert.Contains("No keys here", warnings[1]);
            Assert.Empty(provider.Keymaps.Single());
        }

        [Fact]
        public void invalid_yaml_is_reported_and_other_keymaps_still_load()
        {
            var folder = CreateKeymapFolder(
                "a-broken.yml", "group: [unterminated\n  - : :\n",
                "b-fine.yml", "group: Fine\nprocess:\nshortcuts:\n  - name: Save\n    keys:\n      - Ctrl+S\n");

            var provider = new ShortcutProvider(folder, warnings.Add);

            var warning = Assert.Single(warnings);
            Assert.Contains("a-broken.yml", warning);
            Assert.Equal("Fine", provider.Keymaps.Single().Group);
        }

        [Fact]
        public void keymap_without_group_or_process_applies_to_every_process()
        {
            var folder = CreateKeymapFolder("bare.yml", "shortcuts:\n  - name: Save\n    keys:\n      - Ctrl+S\n");

            var provider = new ShortcutProvider(folder, warnings.Add);

            Assert.Empty(warnings);
            Assert.Equal("Save", provider.GetShortcutsStartingWith(Press("whatever", Keys.S, control: true)).Single().Name);
        }

        [Fact]
        public void top_level_that_is_not_a_mapping_does_not_claim_group_or_process_are_required()
        {
            var folder = CreateKeymapFolder("list.yml", "- one\n- two\n");

            new ShortcutProvider(folder, warnings.Add);

            var warning = Assert.Single(warnings);
            Assert.Contains("list.yml", warning);
            Assert.Contains("top level must be a mapping", warning);
            Assert.Contains("optional", warning);
        }

        [Fact]
        public void keymap_without_a_shortcuts_list_is_reported()
        {
            var folder = CreateKeymapFolder("empty.yml", "group: Empty\nprocess:\n");

            var provider = new ShortcutProvider(folder, warnings.Add);

            var warning = Assert.Single(warnings);
            Assert.Contains("empty.yml", warning);
            Assert.Contains("'shortcuts'", warning);
            Assert.Empty(provider.Keymaps.Single());
        }

        [Fact]
        public void entries_that_are_not_mappings_and_keys_that_are_not_text_are_reported()
        {
            var folder = CreateKeymapFolder("odd2.yml",
                "group: Odd\nprocess:\nshortcuts:\n  - just a string\n  - name: Nested\n    keys:\n      - [Ctrl, S]\n      - Ctrl+D\n");

            var provider = new ShortcutProvider(folder, warnings.Add);

            Assert.Equal(2, warnings.Count);
            Assert.Contains("not a mapping", warnings[0]);
            Assert.Contains("Nested", warnings[1]);
            Assert.Contains("not plain text", warnings[1]);
            // the usable key of the same shortcut still works
            Assert.Equal("Nested", provider.GetShortcutsStartingWith(Press("x", Keys.D, control: true)).Single().Name);
        }
    }
}
