using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Microsoft.Win32;
using NSubstitute;
using SettingsProviderNet;
using Xunit;

namespace Carnac.Tests
{
    // The translation itself is tested against real Windows keyboard layouts (they ship with Windows). A test
    // returns early, as xunit 1.9 cannot skip dynamically, when the system is too old for the translator or a
    // layout cannot be loaded.
    public class KeyboardLayoutTranslatorFacts
    {
        const string Us = "00000409";
        const string German = "00000407";
        const string French = "0000040C";
        const string Russian = "00000419";
        const string Turkish = "0000041F";

        static readonly Keys[] LetterKeys = Enumerable.Range((int)Keys.A, 26).Select(k => (Keys)k).ToArray();
        static readonly Keys[] DigitKeys = Enumerable.Range((int)Keys.D0, 10).Select(k => (Keys)k).ToArray();
        static readonly Keys[] PunctuationKeys =
        {
            Keys.Oem1, Keys.Oemplus, Keys.Oemcomma, Keys.OemMinus, Keys.OemPeriod, Keys.OemQuestion, Keys.Oemtilde,
            Keys.OemOpenBrackets, Keys.Oem5, Keys.Oem6, Keys.Oem7, Keys.OemBackslash
        };

        [Fact]
        public void the_us_layout_names_keys_exactly_as_the_us_tables_do()
        {
            using (var layout = LoadedLayout.Load(Us))
            {
                if (layout == null) return;

                foreach (var key in LetterKeys)
                {
                    Assert.Equal(key.ToString().ToLower(), Translate(key, false, false, layout));
                    Assert.Equal(key.Sanitise(), Translate(key, true, false, layout));
                }

                foreach (var key in DigitKeys.Concat(PunctuationKeys))
                {
                    string shifted;
                    Assert.True(key.SanitiseShift(out shifted), key.ToString());

                    Assert.Equal(key.Sanitise(), Translate(key, false, false, layout));
                    Assert.Equal(shifted, Translate(key, true, false, layout));
                }
            }
        }

        [Fact]
        public void keys_that_do_not_type_a_character_are_not_translated()
        {
            using (var layout = LoadedLayout.Load(Us))
            {
                if (layout == null) return;

                var keys = new[]
                {
                    Keys.Space, Keys.Tab, Keys.Return, Keys.Back, Keys.Escape, Keys.F5, Keys.Left, Keys.Delete,
                    Keys.NumPad1, Keys.Add, Keys.Divide, Keys.LShiftKey, Keys.LControlKey, Keys.LWin
                };

                foreach (var key in keys)
                {
                    Assert.Null(Translate(key, false, false, layout));
                    Assert.Null(Translate(key, true, false, layout));
                }
            }
        }

        [Fact]
        public void the_german_layout_names_its_own_punctuation_and_altgr_characters()
        {
            using (var layout = LoadedLayout.Load(German))
            {
                if (layout == null) return;

                Assert.Equal("ü", Translate(Keys.Oem1, false, false, layout));
                Assert.Equal("Ü", Translate(Keys.Oem1, true, false, layout));
                Assert.Equal("ß", Translate(Keys.OemOpenBrackets, false, false, layout));
                Assert.Equal("/", Translate(Keys.D7, true, false, layout));
                Assert.Equal("=", Translate(Keys.D0, true, false, layout));
                Assert.Equal("@", Translate(Keys.Q, false, true, layout));
                Assert.Equal("{", Translate(Keys.D7, false, true, layout));
                Assert.Equal("\\", Translate(Keys.OemOpenBrackets, false, true, layout));
                Assert.Equal("<", Translate(Keys.OemBackslash, false, false, layout));
                Assert.Equal(",", Translate(Keys.Decimal, false, false, layout));
            }
        }

        [Fact]
        public void the_french_layout_needs_shift_for_digits()
        {
            using (var layout = LoadedLayout.Load(French))
            {
                if (layout == null) return;

                Assert.Equal("&", Translate(Keys.D1, false, false, layout));
                Assert.Equal("1", Translate(Keys.D1, true, false, layout));
                Assert.Equal("é", Translate(Keys.D2, false, false, layout));
                Assert.Equal("2", Translate(Keys.D2, true, false, layout));
            }
        }

        [Fact]
        public void non_latin_and_dotless_letters_follow_the_layout()
        {
            using (var russian = LoadedLayout.Load(Russian))
            using (var turkish = LoadedLayout.Load(Turkish))
            {
                if (russian != null)
                {
                    Assert.Equal("й", Translate(Keys.Q, false, false, russian));
                    Assert.Equal("Й", Translate(Keys.Q, true, false, russian));
                }

                if (turkish != null)
                {
                    Assert.Equal("ı", Translate(Keys.I, false, false, turkish));
                    Assert.Equal("I", Translate(Keys.I, true, false, turkish));
                }
            }
        }

        [Fact]
        public void a_dead_key_is_named_by_its_accent_and_does_not_change_the_keyboard_state()
        {
            using (var layout = LoadedLayout.Load(German))
            {
                if (layout == null) return;

                // Oem6 is the acute accent dead key on the German layout. If translating it were to change the
                // keyboard state, as ToUnicodeEx does without its "do not change" flag, the accent would be combined
                // with the next key and Carnac would eat the dead key the user is typing in another application.
                var accent = Translate(Keys.Oem6, false, false, layout);
                var letterAfterwards = Translate(Keys.A, false, false, layout);
                var accentAgain = Translate(Keys.Oem6, false, false, layout);

                Assert.Equal("´", accent);
                Assert.Equal("a", letterAfterwards);
                Assert.Equal(accent, accentAgain);
            }
        }

        [Fact]
        public void the_translator_uses_the_layout_it_is_given_for_every_call()
        {
            using (var german = LoadedLayout.Load(German))
            using (var french = LoadedLayout.Load(French))
            {
                if (german == null || french == null) return;

                var current = german.Handle;
                var translator = new KeyboardLayoutTranslator(() => current);

                Assert.Equal("ü", translator.GetText(Keys.Oem1, false, false));
                current = french.Handle;
                Assert.Equal("$", translator.GetText(Keys.Oem1, false, false));
            }
        }

        [Fact]
        public void an_unusable_layout_never_throws()
        {
            var translator = new KeyboardLayoutTranslator(() => new IntPtr(1));

            var exception = Record.Exception(() => translator.GetText(Keys.A, false, false));

            Assert.Null(exception);
        }

        [Fact]
        public void the_translator_requires_a_layout_provider()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new KeyboardLayoutTranslator(null));

            Assert.Equal("layoutProvider", exception.ParamName);
        }

        static string Translate(Keys key, bool shift, bool altGr, LoadedLayout layout)
        {
            return KeyboardLayoutTranslator.Translate(key, shift, altGr, layout.Handle);
        }

        sealed class LoadedLayout : IDisposable
        {
            const uint DoNotTellShell = 0x80;

            readonly bool loadedByThisTest;

            LoadedLayout(IntPtr handle, bool loadedByThisTest)
            {
                Handle = handle;
                this.loadedByThisTest = loadedByThisTest;
            }

            public IntPtr Handle { get; private set; }

            // null when the translator is not supported on this system or the layout is not available
            public static LoadedLayout Load(string layoutId)
            {
                if (!KeyboardLayoutTranslator.IsSupported)
                    return null;

                var installed = new IntPtr[256];
                var installedCount = GetKeyboardLayoutList(installed.Length, installed);

                var handle = LoadKeyboardLayout(layoutId, DoNotTellShell);
                if (handle == IntPtr.Zero)
                    return null;

                return new LoadedLayout(handle, !installed.Take(installedCount).Contains(handle));
            }

            public void Dispose()
            {
                if (loadedByThisTest)
                    UnloadKeyboardLayout(Handle);
            }

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            static extern IntPtr LoadKeyboardLayout(string layoutId, uint flags);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool UnloadKeyboardLayout(IntPtr layout);

            [DllImport("user32.dll")]
            static extern int GetKeyboardLayoutList(int size, [Out] IntPtr[] layouts);
        }
    }

    public class KeyProviderLayoutFacts
    {
        readonly IPasswordModeService passwordModeService = new PasswordModeService();
        readonly IDesktopLockEventService desktopLockEventService;
        readonly ISettingsProvider settingsProvider = Substitute.For<ISettingsProvider>();
        readonly FakeKeyboardLayoutTranslator layout = new FakeKeyboardLayoutTranslator();

        public KeyProviderLayoutFacts()
        {
            desktopLockEventService = Substitute.For<IDesktopLockEventService>();
            desktopLockEventService.GetSessionSwitchStream().Returns(Observable.Never<SessionSwitchEventArgs>());
        }

        [Fact]
        public async Task a_typed_key_is_named_by_the_layout()
        {
            layout.Set(Keys.Oem1, false, false, "ü");
            layout.Set(Keys.Oem1, true, false, "Ü");

            Assert.Equal(new[] { "ü" }, await Input(Press(Keys.Oem1)));
            Assert.Equal(new[] { "Ü" }, await Input(Press(Keys.Oem1, shift: true)));
        }

        [Fact]
        public async Task shift_is_not_shown_when_the_layout_names_the_shifted_character()
        {
            layout.Set(Keys.D7, true, false, "/");

            Assert.Equal(new[] { "/" }, await Input(Press(Keys.D7, shift: true)));
        }

        [Fact]
        public async Task keys_the_layout_cannot_name_keep_their_us_names()
        {
            Assert.Equal(new[] { ";" }, await Input(Press(Keys.Oem1)));
            Assert.Equal(new[] { ":" }, await Input(Press(Keys.Oem1, shift: true)));
            Assert.Equal(new[] { "l" }, await Input(Press(Keys.L)));
            Assert.Equal(new[] { "Shift", "Tab" }, await Input(Press(Keys.Tab, shift: true)));
        }

        [Fact]
        public async Task without_a_translator_keys_are_named_as_on_a_us_keyboard()
        {
            layout.Set(Keys.Oem1, false, false, "ü");

            var withoutTranslator = new KeyProvider(Press(Keys.Oem1), passwordModeService, desktopLockEventService, settingsProvider);

            var keyPress = (await withoutTranslator.GetKeyStream().ToList()).Single();
            Assert.Equal(new[] { ";" }, keyPress.Input);
        }

        [Fact]
        public async Task shortcuts_name_punctuation_by_the_layout_but_letters_by_their_latin_name()
        {
            layout.Set(Keys.Oem1, false, false, "ü");
            layout.Set(Keys.L, false, false, "д");
            layout.Set(Keys.L, true, false, "Д");

            Assert.Equal(new[] { "Ctrl", "ü" }, await Input(Press(Keys.Oem1, control: true)));
            Assert.Equal(new[] { "Ctrl", "Shift", "ü" }, await Input(Press(Keys.Oem1, control: true, shift: true)));
            Assert.Equal(new[] { "Alt", "L" }, await Input(Press(Keys.L, alt: true)));
        }

        [Fact]
        public async Task altgr_is_shown_as_the_character_it_types()
        {
            layout.Set(Keys.Q, false, true, "@");

            Assert.Equal(new[] { "@" }, await Input(Press(Keys.Q, control: true, alt: true)));
        }

        [Fact]
        public async Task ctrl_alt_is_a_shortcut_when_the_layout_has_no_altgr_character()
        {
            Assert.Equal(new[] { "Ctrl", "Alt", "Q" }, await Input(Press(Keys.Q, control: true, alt: true)));
        }

        [Fact]
        public async Task the_layout_is_not_consulted_for_the_windows_key()
        {
            layout.Set(Keys.E, false, false, "x");
            var player = KeyStreams.WinkeyE();
            var provider = new KeyProvider(player, passwordModeService, desktopLockEventService, settingsProvider, layout);

            var keyPress = (await provider.GetKeyStream().ToList()).Single();

            Assert.Equal(new[] { "Win", "e" }, keyPress.Input);
        }

        [Fact]
        public void the_translator_may_be_omitted_but_not_the_settings()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new KeyProvider(new KeyPlayer(), passwordModeService, desktopLockEventService, null, layout));

            Assert.Equal("settingsProvider", exception.ParamName);
        }

        async Task<IEnumerable<string>> Input(KeyPlayer player)
        {
            var provider = new KeyProvider(player, passwordModeService, desktopLockEventService, settingsProvider, layout);

            var keyPresses = await provider.GetKeyStream().ToList();

            return keyPresses.Single().Input;
        }

        static KeyPlayer Press(Keys key, bool shift = false, bool control = false, bool alt = false)
        {
            return new KeyPlayer
            {
                new InterceptKeyEventArgs(key, KeyDirection.Down, alt, control, shift),
                new InterceptKeyEventArgs(key, KeyDirection.Up, alt, control, shift)
            };
        }

        class FakeKeyboardLayoutTranslator : IKeyboardLayoutTranslator
        {
            readonly Dictionary<string, string> texts = new Dictionary<string, string>();

            public void Set(Keys key, bool shift, bool altGr, string text)
            {
                texts[Name(key, shift, altGr)] = text;
            }

            public string GetText(Keys key, bool shift, bool altGr)
            {
                string text;
                return texts.TryGetValue(Name(key, shift, altGr), out text) ? text : null;
            }

            static string Name(Keys key, bool shift, bool altGr)
            {
                return key + "|" + shift + "|" + altGr;
            }
        }
    }
}
