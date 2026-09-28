using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Microsoft.Win32;
using NSubstitute;
using SettingsProviderNet;
using Xunit;
using Message = Carnac.Logic.Models.Message;

namespace Carnac.Tests
{
    // The translation itself is tested against real Windows keyboard layouts (they ship with Windows). Where a test
    // depends on a layout that could not be loaded it returns early, as xunit 1.9 cannot skip dynamically; the layouts
    // the tests rely on most are checked to be loadable on a system that supports the translator, so a machine
    // that cannot load them fails instead of passing without testing anything.
    public class KeyboardLayoutTranslatorFacts
    {
        const string Us = "00000409";
        const string German = "00000407";
        const string French = "0000040C";
        const string Russian = "00000419";
        const string Turkish = "0000041F";
        const string PortugueseBrazil = "00000416";
        const string PersianStandard = "00050429";
        const string Sinhala = "0000045B";

        static readonly Keys[] LetterKeys = Enumerable.Range((int)Keys.A, 26).Select(k => (Keys)k).ToArray();
        static readonly Keys[] DigitKeys = Enumerable.Range((int)Keys.D0, 10).Select(k => (Keys)k).ToArray();
        static readonly Keys[] PunctuationKeys =
        {
            Keys.Oem1, Keys.Oemplus, Keys.Oemcomma, Keys.OemMinus, Keys.OemPeriod, Keys.OemQuestion, Keys.Oemtilde,
            Keys.OemOpenBrackets, Keys.Oem5, Keys.Oem6, Keys.Oem7, Keys.OemBackslash
        };

        [Fact]
        public void the_layouts_these_tests_rely_on_can_be_loaded_where_the_translator_is_supported()
        {
            if (!KeyboardLayoutTranslator.IsSupported) return;

            foreach (var layoutId in new[] { Us, German, French, Russian, Turkish, PortugueseBrazil })
            {
                using (var layout = LoadedLayout.Load(layoutId))
                {
                    Assert.True(layout != null, "the keyboard layout " + layoutId + " cannot be loaded, so the tests that use it would test nothing");
                }
            }
        }

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
        public void the_extra_keys_of_brazilian_keyboards_are_character_keys()
        {
            var abntC1 = (Keys)0xC1;
            var abntC2 = (Keys)0xC2;
            Assert.True(KeyboardLayoutTranslator.IsCharacterKey(abntC1));
            Assert.True(KeyboardLayoutTranslator.IsCharacterKey(abntC2));

            using (var layout = LoadedLayout.Load(PortugueseBrazil))
            {
                if (layout == null) return;

                Assert.Equal("/", Translate(abntC1, false, false, layout));
                Assert.Equal("?", Translate(abntC1, true, false, layout));
                Assert.Equal(".", Translate(abntC2, false, false, layout));
            }
        }

        [Fact]
        public void characters_nobody_can_see_are_not_names_of_keys()
        {
            // Persian types a zero width non-joiner on Shift+B, Sinhala a no-break space on Shift+the backslash key
            using (var persian = LoadedLayout.Load(PersianStandard))
            using (var sinhala = LoadedLayout.Load(Sinhala))
            {
                if (persian != null)
                    Assert.Null(Translate(Keys.B, true, false, persian));

                if (sinhala != null)
                    Assert.Null(Translate(Keys.Oem5, true, false, sinhala));
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
        public void a_dead_key_typed_in_the_application_is_combined_but_not_used_up()
        {
            using (var layout = LoadedLayout.Load(German))
            {
                if (layout == null) return;

                try
                {
                    // the user types the acute accent in an application: Windows keeps it until the next key
                    LoadedLayout.TypeDeadKey(layout.Handle, Keys.Oem6);

                    var first = Translate(Keys.A, false, false, layout);
                    var second = Translate(Keys.A, false, false, layout);

                    Assert.Equal("á", first);
                    // Carnac looked, the accent is still there for the application to combine
                    Assert.Equal(first, second);
                }
                finally
                {
                    LoadedLayout.ClearPendingDeadKey(layout.Handle);
                }
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
        public void ctrl_and_alt_are_altgr_only_when_the_right_alt_key_is_down()
        {
            using (var layout = LoadedLayout.Load(German))
            {
                if (layout == null) return;

                var rightAltDown = false;
                var translator = new KeyboardLayoutTranslator(() => layout.Handle, () => rightAltDown);

                // Ctrl+Alt+Q with the left Alt key is a shortcut, not the @ that AltGr+Q types
                Assert.Null(translator.GetText(Keys.Q, false, true));

                rightAltDown = true;
                Assert.Equal("@", translator.GetText(Keys.Q, false, true));

                // without Ctrl+Alt the right Alt key is of no interest
                rightAltDown = false;
                Assert.Equal("q", translator.GetText(Keys.Q, false, false));
            }
        }

        [Fact]
        public void without_a_layout_nothing_is_translated()
        {
            var translator = new KeyboardLayoutTranslator(() => IntPtr.Zero);

            Assert.Null(translator.GetText(Keys.A, false, false));
        }

        [Fact]
        public void an_unusable_layout_never_throws()
        {
            var translator = new KeyboardLayoutTranslator(() => new IntPtr(1));

            var exception = Record.Exception(() => translator.GetText(Keys.A, false, false));

            Assert.Null(exception);
        }

        [Fact]
        public void the_layout_of_the_window_that_has_the_focus_never_throws()
        {
            var translator = new KeyboardLayoutTranslator();

            string text = null;
            var exception = Record.Exception(() => { text = translator.GetText(Keys.A, false, false); KeyboardLayoutTranslator.GetFocusedWindowLayout(); });

            Assert.Null(exception);
            // no focus at all (a locked desktop) is no layout; otherwise it is the letter of the focused layout
            Assert.True(text == null || text.Length >= 1);
        }

        [Fact]
        public void the_translator_requires_what_it_works_with()
        {
            Assert.Equal("layoutProvider", Assert.Throws<ArgumentNullException>(() => new KeyboardLayoutTranslator(null)).ParamName);
            Assert.Equal("isRightAltDown", Assert.Throws<ArgumentNullException>(() => new KeyboardLayoutTranslator(() => IntPtr.Zero, null)).ParamName);
        }

        static string Translate(Keys key, bool shift, bool altGr, LoadedLayout layout)
        {
            return KeyboardLayoutTranslator.Translate(key, shift, altGr, layout.Handle);
        }

        sealed class LoadedLayout : IDisposable
        {
            const uint DoNotTellShell = 0x80;
            const uint SpaceKey = 0x20;

            LoadedLayout(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; private set; }

            // null when the translator is not supported on this system or the layout is not available
            public static LoadedLayout Load(string layoutId)
            {
                if (!KeyboardLayoutTranslator.IsSupported)
                    return null;

                // Not unloaded afterwards: the list of layouts is shared with the other programs of the session, and the
                // layout goes away by itself when the last program that uses it ends.
                var handle = LoadKeyboardLayout(layoutId, DoNotTellShell);
                return handle == IntPtr.Zero ? null : new LoadedLayout(handle);
            }

            // What an application does when the user types a dead key: ToUnicodeEx without the "do not change" flag
            public static void TypeDeadKey(IntPtr layout, Keys deadKey)
            {
                var text = new char[8];
                ToUnicodeEx((uint)deadKey, MapVirtualKeyEx((uint)deadKey, 0, layout), new byte[256], text, text.Length, 0, layout);
            }

            // ... and the space that follows uses the pending accent up
            public static void ClearPendingDeadKey(IntPtr layout)
            {
                var text = new char[8];
                ToUnicodeEx(SpaceKey, MapVirtualKeyEx(SpaceKey, 0, layout), new byte[256], text, text.Length, 0, layout);
            }

            public void Dispose()
            {
            }

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            static extern IntPtr LoadKeyboardLayout(string layoutId, uint flags);

            [DllImport("user32.dll")]
            static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr layout);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            static extern int ToUnicodeEx(uint virtualKey, uint scanCode, byte[] keyState, [Out] char[] buffer, int bufferSize, uint flags, IntPtr layout);
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
        public async Task shortcuts_name_punctuation_by_the_layout_but_letters_and_digits_by_their_latin_name()
        {
            layout.Set(Keys.Oem1, false, false, "ü");
            layout.Set(Keys.L, false, false, "д");
            layout.Set(Keys.D1, false, false, "&");

            Assert.Equal(new[] { "Ctrl", "ü" }, await Input(Press(Keys.Oem1, control: true)));
            Assert.Equal(new[] { "Ctrl", "Shift", "ü" }, await Input(Press(Keys.Oem1, control: true, shift: true)));
            Assert.Equal(new[] { "Alt", "L" }, await Input(Press(Keys.L, alt: true)));
            Assert.Equal(new[] { "Ctrl", "1" }, await Input(Press(Keys.D1, control: true)));
        }

        [Fact]
        public async Task altgr_is_shown_as_the_character_it_types()
        {
            layout.Set(Keys.Q, false, true, "@");

            Assert.Equal(new[] { "@" }, await Input(Press(Keys.Q, control: true, alt: true)));
        }

        [Fact]
        public async Task a_character_typed_with_altgr_is_text_that_merges_with_the_text_around_it()
        {
            layout.Set(Keys.Q, false, true, "@");
            var keys = new KeyPlayer();
            keys.AddRange(Press(Keys.A));
            keys.AddRange(Press(Keys.Q, control: true, alt: true));
            keys.AddRange(Press(Keys.B));

            var keyPresses = await new KeyProvider(keys, passwordModeService, desktopLockEventService, settingsProvider, layout).GetKeyStream().ToList();
            Assert.NotEmpty(keyPresses);
            var process = new ProcessInfo("FakeProcess");
            var messages = keyPresses.Select(k => new Message(new KeyPress(process, k.InterceptKeyEventArgs, false, k.Input))).ToList();

            Assert.False(keyPresses[1].HasModifierPressed);
            Assert.True(messages[1].CanBeMerged);
            Assert.False(messages[1].IsModifier);
            var text = messages.Aggregate((merged, next) => merged.Merge(next));
            Assert.Equal("a@b", string.Join(string.Empty, text.Text));
        }

        [Fact]
        public async Task ctrl_and_alt_are_a_shortcut_when_the_layout_has_no_altgr_character()
        {
            Assert.Equal(new[] { "Ctrl", "Alt", "Q" }, await Input(Press(Keys.Q, control: true, alt: true)));
        }

        [Fact]
        public async Task win_and_a_punctuation_key_are_named_by_the_layout()
        {
            layout.Set(Keys.Oem1, false, false, "ü");
            layout.Set(Keys.D7, true, false, "/");
            layout.Set(Keys.D1, false, false, "&");
            layout.Set(Keys.E, false, false, "x");

            Assert.Equal(new[] { "Win", "ü" }, await Input(WinPress(Keys.Oem1)));
            Assert.Equal(new[] { "Win", "/" }, await Input(WinPress(Keys.D7, shift: true)));
            // the digit and the letter are named as in the shortcut lists
            Assert.Equal(new[] { "Win", "1" }, await Input(WinPress(Keys.D1)));
            Assert.Equal(new[] { "Win", "e" }, await Input(WinPress(Keys.E)));
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

        static KeyPlayer WinPress(Keys key, bool shift = false)
        {
            var keys = new KeyPlayer { new InterceptKeyEventArgs(Keys.LWin, KeyDirection.Down, false, false, false) };
            keys.AddRange(Press(key, shift));
            keys.Add(new InterceptKeyEventArgs(Keys.LWin, KeyDirection.Up, false, false, false));
            return keys;
        }

        class FakeKeyboardLayoutTranslator : IKeyboardLayoutTranslator
        {
            readonly Dictionary<string, string> texts = new Dictionary<string, string>();

            public void Set(Keys key, bool shift, bool controlAlt, string text)
            {
                texts[Name(key, shift, controlAlt)] = text;
            }

            public string GetText(Keys key, bool shift, bool controlAlt)
            {
                string text;
                return texts.TryGetValue(Name(key, shift, controlAlt), out text) ? text : null;
            }

            static string Name(Keys key, bool shift, bool controlAlt)
            {
                return key + "|" + shift + "|" + controlAlt;
            }
        }
    }
}
