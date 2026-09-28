using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
    public class LowLevelAltKeyFacts
    {
        [Fact]
        public void the_alt_keys_go_down_and_up_as_system_keys()
        {
            foreach (var alt in new[] { Keys.LMenu, Keys.RMenu })
            {
                Assert.Equal(KeyDirection.Down, Create(alt, Win32Methods.WM_SYSKEYDOWN).KeyDirection);
                Assert.Equal(KeyDirection.Up, Create(alt, Win32Methods.WM_SYSKEYUP).KeyDirection);
                Assert.Equal(KeyDirection.Up, Create(alt, Win32Methods.WM_KEYUP).KeyDirection);
            }
        }

        [Fact]
        public void other_keys_are_reported_as_before()
        {
            Assert.Equal(KeyDirection.Down, Create(Keys.LControlKey, Win32Methods.WM_KEYDOWN).KeyDirection);
            Assert.Equal(KeyDirection.Up, Create(Keys.LControlKey, Win32Methods.WM_KEYUP).KeyDirection);

            var keyPressedWithAlt = Create(Keys.A, Win32Methods.WM_SYSKEYDOWN);
            Assert.Equal(KeyDirection.Down, keyPressedWithAlt.KeyDirection);
            Assert.True(keyPressedWithAlt.AltPressed);
        }

        static InterceptKeyEventArgs Create(Keys key, int windowMessage)
        {
            var keyboardHookData = Marshal.AllocHGlobal(32);
            try
            {
                Marshal.WriteInt32(keyboardHookData, (int)key);
                return InterceptKeys.CreateEventArgs(new IntPtr(windowMessage), keyboardHookData);
            }
            finally
            {
                Marshal.FreeHGlobal(keyboardHookData);
            }
        }
    }

    public class ModifierKeyPressFacts
    {
        readonly IPasswordModeService passwordModeService = new PasswordModeService();
        readonly IDesktopLockEventService desktopLockEventService = Substitute.For<IDesktopLockEventService>();
        readonly ISettingsProvider settingsProvider = Substitute.For<ISettingsProvider>();
        readonly PopupSettings settings = new PopupSettings { ShowModifierKeyPresses = true };
        readonly Subject<SessionSwitchEventArgs> sessionSwitches = new Subject<SessionSwitchEventArgs>();

        public ModifierKeyPressFacts()
        {
            desktopLockEventService.GetSessionSwitchStream().Returns(sessionSwitches);
            settingsProvider.GetSettings<PopupSettings>().Returns(settings);
        }

        [Fact]
        public async Task modifiers_on_their_own_are_not_shown_by_default()
        {
            settings.ShowModifierKeyPresses = false;

            var keyPresses = await Play(Down(Keys.LControlKey), Up(Keys.LControlKey), Down(Keys.LMenu), Down(Keys.LWin));

            Assert.Empty(keyPresses);
        }

        [Fact]
        public async Task ctrl_alt_and_win_are_shown_when_pressed_on_their_own()
        {
            var keyPresses = await Play(Down(Keys.LControlKey), Up(Keys.LControlKey), Down(Keys.RMenu), Up(Keys.RMenu), Down(Keys.RWin), Up(Keys.RWin));

            Assert.Equal(new[] { "Ctrl", "Alt", "Win" }, keyPresses.Select(k => string.Join(" + ", k.Input)).ToArray());
            Assert.True(keyPresses.All(k => k.IsModifierOnly));
        }

        [Fact]
        public async Task the_modifier_flags_of_the_key_press_come_from_the_held_keys()
        {
            var keyPresses = await Play(Down(Keys.LControlKey), Down(Keys.LShiftKey), Down(Keys.LMenu));

            var chord = keyPresses.Last();
            Assert.Equal(new[] { "Ctrl", "Alt", "Shift" }, chord.Input.ToArray());
            Assert.True(chord.InterceptKeyEventArgs.ControlPressed);
            Assert.True(chord.InterceptKeyEventArgs.AltPressed);
            Assert.True(chord.InterceptKeyEventArgs.ShiftPressed);
            Assert.True(chord.HasModifierPressed);
        }

        [Fact]
        public async Task shift_on_its_own_is_not_shown()
        {
            var keyPresses = await Play(Down(Keys.LShiftKey), Up(Keys.LShiftKey), Down(Keys.RShiftKey), Up(Keys.RShiftKey));

            Assert.Empty(keyPresses);
        }

        [Fact]
        public async Task shift_is_shown_with_the_other_modifiers_in_whichever_order_they_are_pressed()
        {
            var ctrlThenShift = await Play(Down(Keys.LControlKey), Down(Keys.LShiftKey));
            var shiftThenCtrl = await new ModifierKeyPressFacts().Play(Down(Keys.LShiftKey), Down(Keys.LControlKey));

            Assert.Equal(new[] { "Ctrl", "Ctrl + Shift" }, ctrlThenShift.Select(k => string.Join(" + ", k.Input)).ToArray());
            Assert.Equal(new[] { "Ctrl + Shift" }, shiftThenCtrl.Select(k => string.Join(" + ", k.Input)).ToArray());
        }

        [Fact]
        public async Task a_held_modifier_that_repeats_is_shown_once()
        {
            var keyPresses = await Play(Down(Keys.LControlKey), Down(Keys.LControlKey), Down(Keys.LControlKey), Down(Keys.LControlKey));

            Assert.Equal(1, keyPresses.Count);
        }

        [Fact]
        public async Task a_modifier_is_shown_again_when_it_is_pressed_again()
        {
            var keyPresses = await Play(Down(Keys.LControlKey), Down(Keys.LControlKey), Up(Keys.LControlKey), Down(Keys.LControlKey));

            Assert.Equal(2, keyPresses.Count);
        }

        [Fact]
        public async Task the_left_and_right_key_of_a_modifier_are_tracked_separately()
        {
            var keyPresses = await Play(Down(Keys.LControlKey), Down(Keys.RControlKey), Up(Keys.LControlKey), Down(Keys.LControlKey));

            // the right Ctrl is a new press while the left one is held, and the left Ctrl goes down again after its release
            Assert.Equal(3, keyPresses.Count);
        }

        [Fact]
        public void the_held_keys_are_forgotten_when_the_session_is_locked()
        {
            var keys = new Subject<InterceptKeyEventArgs>();
            var keyPresses = new List<KeyPress>();
            var source = Substitute.For<IInterceptKeys>();
            source.GetKeyStream().Returns(keys);
            using (new KeyProvider(source, passwordModeService, desktopLockEventService, settingsProvider).GetKeyStream().Subscribe(keyPresses.Add))
            {
                keys.OnNext(Down(Keys.LControlKey));
                // the key up is never seen when the desktop is locked
                sessionSwitches.OnNext(new SessionSwitchEventArgs(SessionSwitchReason.SessionLock));
                keys.OnNext(Down(Keys.LControlKey));
            }

            Assert.Equal(2, keyPresses.Count);
        }

        [Fact]
        public async Task a_key_pressed_with_a_modifier_is_shown_after_the_modifier()
        {
            var keyPresses = await Play(Down(Keys.LControlKey), Down(Keys.L, control: true), Up(Keys.L, control: true), Up(Keys.LControlKey));

            Assert.Equal(new[] { "Ctrl", "Ctrl + L" }, keyPresses.Select(k => string.Join(" + ", k.Input)).ToArray());
            Assert.False(keyPresses.Last().IsModifierOnly);
        }

        [Fact]
        public async Task the_windows_key_still_works_as_a_modifier()
        {
            var keyPresses = await Play(Down(Keys.LWin), Down(Keys.E), Up(Keys.E), Up(Keys.LWin));

            Assert.Equal(new[] { "Win", "Win + e" }, keyPresses.Select(k => string.Join(" + ", k.Input)).ToArray());
            Assert.True(keyPresses.First().WinkeyPressed);
        }

        [Fact]
        public async Task password_mode_hides_modifiers_too()
        {
            var passwordMode = Substitute.For<IPasswordModeService>();
            passwordMode.CheckPasswordMode(Arg.Any<InterceptKeyEventArgs>()).Returns(true);

            var keyPresses = await Play(passwordMode, Down(Keys.LControlKey), Down(Keys.LMenu));

            Assert.Empty(keyPresses);
        }

        [Fact]
        public async Task the_process_filter_applies_to_modifiers()
        {
            settings.ProcessFilterExpression = "notepad";

            var keyPresses = await Play(Down(Keys.LControlKey));

            Assert.Empty(keyPresses);
        }

        static InterceptKeyEventArgs Down(Keys key, bool control = false, bool alt = false, bool shift = false)
        {
            return new InterceptKeyEventArgs(key, KeyDirection.Down, alt, control, shift);
        }

        static InterceptKeyEventArgs Up(Keys key, bool control = false, bool alt = false, bool shift = false)
        {
            return new InterceptKeyEventArgs(key, KeyDirection.Up, alt, control, shift);
        }

        Task<IList<KeyPress>> Play(params InterceptKeyEventArgs[] keys)
        {
            return Play(passwordModeService, keys);
        }

        async Task<IList<KeyPress>> Play(IPasswordModeService passwordMode, params InterceptKeyEventArgs[] keys)
        {
            var player = new KeyPlayer();
            player.AddRange(keys);
            var provider = new KeyProvider(player, passwordMode, desktopLockEventService, settingsProvider);

            return await provider.GetKeyStream().ToList();
        }
    }

    public class ModifierMessageFacts
    {
        readonly ProcessInfo process = new ProcessInfo("FakeProcess");

        [Fact]
        public void a_modifier_on_its_own_is_a_message_that_is_never_merged()
        {
            var ctrl = new Message(Chord("Ctrl"));

            Assert.True(ctrl.IsModifierOnly);
            Assert.True(ctrl.IsModifier);
            Assert.False(ctrl.CanBeMerged);
            Assert.Equal("Ctrl", TextOf(ctrl));
        }

        [Fact]
        public void a_key_pressed_with_the_modifier_takes_its_place()
        {
            var ctrl = new Message(Chord("Ctrl"));
            var ctrlC = new Message(Key(Keys.C, true, "Ctrl", "C"));

            var result = Message.MergeIfNeeded(ctrl, ctrlC);

            Assert.Same(ctrl, result.Previous);
            Assert.Equal("Ctrl + C", TextOf(result));
            Assert.False(result.IsModifierOnly);
        }

        [Fact]
        public void a_bigger_chord_takes_the_place_of_the_modifiers_it_grows_from()
        {
            var ctrl = new Message(Chord("Ctrl"));
            var ctrlShift = new Message(Chord("Ctrl", "Shift"));

            var chord = Message.MergeIfNeeded(ctrl, ctrlShift);
            var key = Message.MergeIfNeeded(chord, new Message(Key(Keys.L, true, "Ctrl", "Shift", "L")));

            Assert.Same(ctrl, chord.Previous);
            Assert.Equal("Ctrl + Shift", TextOf(chord));
            Assert.Same(chord, key.Previous);
            Assert.Equal("Ctrl + Shift + L", TextOf(key));
        }

        [Fact]
        public void pressing_the_same_modifier_again_replaces_the_message_instead_of_repeating_it()
        {
            var ctrl = new Message(Chord("Ctrl"));

            var result = Message.MergeIfNeeded(ctrl, new Message(Chord("Ctrl")));

            Assert.Same(ctrl, result.Previous);
            Assert.Equal("Ctrl", TextOf(result));
        }

        [Fact]
        public void typing_after_the_modifier_was_released_does_not_take_its_place()
        {
            var win = new Message(Chord("Win"));
            var typed = new Message(Key(Keys.N, false, "n"));

            var result = Message.MergeIfNeeded(win, typed);

            Assert.Null(result.Previous);
            Assert.Equal("n", TextOf(result));
        }

        [Fact]
        public void a_different_modifier_does_not_take_its_place()
        {
            var ctrl = new Message(Chord("Ctrl"));
            var alt = new Message(Chord("Alt"));

            var result = Message.MergeIfNeeded(ctrl, alt);

            Assert.Null(result.Previous);
            Assert.Equal("Alt", TextOf(result));
        }

        [Fact]
        public void a_key_from_another_process_does_not_take_the_place_of_the_modifier()
        {
            var ctrl = new Message(Chord("Ctrl"));
            var other = new Message(new KeyPress(new ProcessInfo("Other"), new InterceptKeyEventArgs(Keys.C, KeyDirection.Down, false, true, false), false, new[] { "Ctrl", "C" }));

            var result = Message.MergeIfNeeded(ctrl, other);

            Assert.Null(result.Previous);
        }

        [Fact]
        public void a_shortcut_that_takes_the_place_of_the_modifier_keeps_its_name()
        {
            var ctrl = new Message(Chord("Ctrl"));
            var shortcut = new Message(new[] { Key(Keys.S, true, "Ctrl", "S") }, new KeyShortcut("Save"), true);

            var result = Message.MergeIfNeeded(ctrl, shortcut);

            Assert.Same(ctrl, result.Previous);
            Assert.True(result.IsShortcut);
            Assert.Equal("Save", result.ShortcutName);
        }

        [Fact]
        public void a_message_with_a_key_and_a_modifier_is_not_modifier_only()
        {
            Assert.False(new Message(Key(Keys.C, true, "Ctrl", "C")).IsModifierOnly);
            Assert.False(new Message(Key(Keys.C, false, "c")).IsModifierOnly);
        }

        KeyPress Chord(params string[] names)
        {
            var control = names.Contains("Ctrl");
            var alt = names.Contains("Alt");
            var shift = names.Contains("Shift");
            var key = names.Contains("Win") ? Keys.LWin : alt ? Keys.LMenu : Keys.LControlKey;
            return new KeyPress(process, new InterceptKeyEventArgs(key, KeyDirection.Down, alt, control, shift), names.Contains("Win"), names);
        }

        KeyPress Key(Keys key, bool control, params string[] input)
        {
            return new KeyPress(process, new InterceptKeyEventArgs(key, KeyDirection.Down, false, control, false), false, input);
        }

        static string TextOf(Message message)
        {
            return string.Join(string.Empty, message.Text);
        }
    }

    public class ModifierMessageProviderFacts
    {
        readonly IShortcutProvider shortcutProvider = Substitute.For<IShortcutProvider>();
        readonly PopupSettings settings = new PopupSettings { ShowModifierKeyPresses = true };

        public ModifierMessageProviderFacts()
        {
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>()).Returns(new List<KeyShortcut>());
        }

        [Fact]
        public async Task pressing_ctrl_again_does_not_break_a_shortcut_sequence()
        {
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>())
                .Returns(new List<KeyShortcut> { new KeyShortcut("Comment", new KeyPressDefinition(Keys.K, controlPressed: true), new KeyPressDefinition(Keys.C, controlPressed: true)) });
            var keys = new KeyPlayer
            {
                new InterceptKeyEventArgs(Keys.LControlKey, KeyDirection.Down, false, false, false),
                new InterceptKeyEventArgs(Keys.K, KeyDirection.Down, false, true, false),
                new InterceptKeyEventArgs(Keys.K, KeyDirection.Up, false, true, false),
                new InterceptKeyEventArgs(Keys.LControlKey, KeyDirection.Up, false, true, false),
                // Ctrl is released and pressed again between the two chords, and auto-repeats while it is held
                new InterceptKeyEventArgs(Keys.LControlKey, KeyDirection.Down, false, false, false),
                new InterceptKeyEventArgs(Keys.LControlKey, KeyDirection.Down, false, true, false),
                new InterceptKeyEventArgs(Keys.C, KeyDirection.Down, false, true, false),
                new InterceptKeyEventArgs(Keys.C, KeyDirection.Up, false, true, false),
                new InterceptKeyEventArgs(Keys.LControlKey, KeyDirection.Up, false, true, false)
            };

            var messages = await CreateMessageProvider(keys).GetMessageStream().ToList();

            var sequence = messages.Single(m => m.IsShortcut);
            Assert.Equal("Comment", sequence.ShortcutName);
            Assert.Equal("Ctrl + K, Ctrl + C [Comment]", string.Join(string.Empty, sequence.Text));
        }

        [Fact]
        public async Task the_modifier_is_replaced_by_the_shortcut_it_is_held_for()
        {
            var messages = await CreateMessageProvider(KeyStreams.CtrlU()).GetMessageStream().ToList();

            Assert.Equal(2, messages.Count);
            Assert.Equal("Ctrl", string.Join(string.Empty, messages[0].Text));
            Assert.Equal("Ctrl + U", string.Join(string.Empty, messages[1].Text));
            Assert.Same(messages[0], messages[1].Previous);
        }

        [Fact]
        public async Task without_the_setting_nothing_changes()
        {
            settings.ShowModifierKeyPresses = false;

            var messages = await CreateMessageProvider(KeyStreams.CtrlU()).GetMessageStream().ToList();

            Assert.Equal(1, messages.Count);
            Assert.Equal("Ctrl + U", string.Join(string.Empty, messages[0].Text));
            Assert.Null(messages[0].Previous);
        }

        [Fact]
        public async Task modifiers_pass_the_only_keys_with_modifiers_filter_but_not_the_shortcuts_only_filter()
        {
            settings.ShowOnlyModifiers = true;
            var withModifiers = await CreateMessageProvider(KeyStreams.CtrlU()).GetMessageStream().ToList();

            settings.ShowOnlyModifiers = false;
            settings.DetectShortcutsOnly = true;
            var shortcutsOnly = await CreateMessageProvider(KeyStreams.CtrlU()).GetMessageStream().ToList();

            Assert.Equal(2, withModifiers.Count);
            Assert.Empty(shortcutsOnly);
        }

        MessageProvider CreateMessageProvider(IEnumerable<InterceptKeyEventArgs> keys)
        {
            var source = Substitute.For<IInterceptKeys>();
            source.GetKeyStream().Returns(keys.ToObservable());
            var desktopLockEventService = Substitute.For<IDesktopLockEventService>();
            desktopLockEventService.GetSessionSwitchStream().Returns(Observable.Never<SessionSwitchEventArgs>());
            var settingsProvider = Substitute.For<ISettingsProvider>();
            settingsProvider.GetSettings<PopupSettings>().Returns(settings);
            var keyProvider = new KeyProvider(source, new PasswordModeService(), desktopLockEventService, settingsProvider);
            return new MessageProvider(shortcutProvider, keyProvider, settings);
        }
    }
}
