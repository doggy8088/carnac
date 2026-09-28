using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.Enums;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Microsoft.Win32;
using NSubstitute;
using SettingsProviderNet;
using Xunit;
using Message = Carnac.Logic.Models.Message;

namespace Carnac.Tests
{
    public class MessageProviderFacts
    {
        readonly IShortcutProvider shortcutProvider;

        public MessageProviderFacts()
        {
            shortcutProvider = Substitute.For<IShortcutProvider>();
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>()).Returns(new List<KeyShortcut>());
            
        }

        MessageProvider CreateMessageProvider(IObservable<InterceptKeyEventArgs> keysStreamSource)
        {
            var source = Substitute.For<IInterceptKeys>();
            source.GetKeyStream().Returns(keysStreamSource);
            var desktopLockEventService = Substitute.For<IDesktopLockEventService>();
            var settingsProvider = Substitute.For<ISettingsProvider>();
            desktopLockEventService.GetSessionSwitchStream().Returns(Observable.Never<SessionSwitchEventArgs>());
            var keyProvider = new KeyProvider(source, new PasswordModeService(), desktopLockEventService, settingsProvider);
            return new MessageProvider(shortcutProvider, keyProvider, new PopupSettings());
        }

        [Fact]
        public async Task key_with_modifiers_raises_a_new_message()
        {
            // arrange
            var keySequence = KeyStreams.LetterL()
                .Concat(KeyStreams.CtrlShiftL())
                .ToObservable();
            var sut = CreateMessageProvider(keySequence);

            // act
            var messages = await sut.GetMessageStream().ToList();

            // assert
            Assert.Equal(2, messages.Count);
        }

        [Fact]
        public async Task recognises_shortcuts()
        {
            // arrange
            var keySequence = KeyStreams.CtrlShiftL().ToObservable();
            var sut = CreateMessageProvider(keySequence);
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>())
                .Returns(new List<KeyShortcut> { new KeyShortcut("MyShortcut", new KeyPressDefinition(Keys.L, shiftPressed: true, controlPressed: true)) });

            // act
            var messages = await sut.GetMessageStream().ToList();

            // assert
            Assert.Equal(1, messages.Count);
            Assert.Equal("MyShortcut", messages[0].ShortcutName);
        }

        [Fact]
        public async Task does_not_show_key_press_on_partial_match()
        {
            // arrange
            var keySequence = KeyStreams.CtrlU().ToObservable();
            var sut = CreateMessageProvider(keySequence);
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>())
                .Returns(new List<KeyShortcut> { new KeyShortcut("SomeShortcut",
                    new KeyPressDefinition(Keys.U, controlPressed: true),
                    new KeyPressDefinition(Keys.L)) });

            // act
            var messages = await sut.GetMessageStream().ToList();

            // assert
            Assert.Equal(0, messages.Count);
        }

        [Fact]
        public async Task produces_two_messages_when_shortcut_is_broken()
        {
            // arrange
            var keySequence = KeyStreams.CtrlU()
                .Concat(KeyStreams.Number1())
                .ToObservable();
            var sut = CreateMessageProvider(keySequence);
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>())
                .Returns(new List<KeyShortcut> { new KeyShortcut("SomeShortcut",
                    new KeyPressDefinition(Keys.U, controlPressed: true),
                    new KeyPressDefinition(Keys.L)) });

            // act
            var messages = await sut.GetMessageStream().ToList();

            // assert
            Assert.Equal(2, messages.Count);
            Assert.Equal("Ctrl + U", string.Join("", messages[0].Text));
            Assert.Equal("1", string.Join("", messages[1].Text));
        }

        [Fact]
        public async Task does_show_shortcut_name_on_full_match()
        {
            // arrange
            var keySequence = KeyStreams.CtrlU()
                .Concat(KeyStreams.LetterL())
                .ToObservable();
            var sut = CreateMessageProvider(keySequence);
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>())
                .Returns(new List<KeyShortcut> { new KeyShortcut("SomeShortcut",
                    new KeyPressDefinition(Keys.U, controlPressed: true),
                    new KeyPressDefinition(Keys.L)) });

            // act
            var messages = await sut.GetMessageStream().ToList();

            // assert
            Assert.Equal(1, messages.Count);
            Assert.Equal("SomeShortcut", messages[0].ShortcutName);
        }

        [Fact]
        public async Task keeps_order_of_streams()
        {
            // arrange
            var keySequence = KeyStreams.CtrlU()
                .Concat(KeyStreams.LetterL())
                .Concat(KeyStreams.Number1())
                .Concat(KeyStreams.LetterL())
                .ToObservable();
            var sut = CreateMessageProvider(keySequence);
            shortcutProvider
                .GetShortcutsStartingWith(Arg.Any<KeyPress>())
                .Returns(new List<KeyShortcut> { new KeyShortcut("SomeShortcut",
                    new KeyPressDefinition(Keys.U, controlPressed: true),
                    new KeyPressDefinition(Keys.L)) });

            // act
            var messages = await sut.GetMessageStream().ToList();

            // assert
            Assert.Equal(3, messages.Count);
            Assert.Equal("Ctrl + U, l [SomeShortcut]", string.Join("", messages[0].Text));
            Assert.Equal("SomeShortcut", messages[0].ShortcutName);
            Assert.Equal("1", string.Join("", messages[1].Text));
            Assert.Equal("1l", string.Join("", messages[2].Text));
        }

        [Fact]
        public async Task typed_repeats_are_summarised_from_the_fourth_press_by_default()
        {
            var sut = CreateMessageProvider(new PopupSettings(), Typed("l", "l", "l", "l").ToObservable());

            var messages = await sut.GetMessageStream().ToList();

            Assert.Equal(4, messages.Count);
            Assert.Equal("lll", TextOf(messages[2]));
            Assert.Equal("l x 4 ", TextOf(messages[3]));
        }

        [Fact]
        public async Task typed_repeats_are_shown_as_typed_when_grouping_is_off()
        {
            var settings = new PopupSettings { RepeatedKeyGrouping = RepeatedKeyGrouping.Never };
            var sut = CreateMessageProvider(settings, Typed("l", "l", "l", "l", "l", "l").ToObservable());

            var messages = await sut.GetMessageStream().ToList();

            Assert.Equal("llllll", TextOf(messages.Last()));
        }

        [Fact]
        public async Task the_threshold_setting_decides_when_typed_repeats_are_summarised()
        {
            var settings = new PopupSettings { RepeatedKeyThreshold = 2 };
            var sut = CreateMessageProvider(settings, Typed("h", "e", "l", "l", "o").ToObservable());

            var messages = await sut.GetMessageStream().ToList();

            Assert.Equal("hel x 2 o", TextOf(messages.Last()));
        }

        [Fact]
        public void a_change_of_the_grouping_setting_applies_to_the_next_key_press()
        {
            var settings = new PopupSettings();
            var keys = new Subject<KeyPress>();
            var messages = new List<Message>();
            CreateMessageProvider(settings, keys).GetMessageStream().Subscribe(messages.Add);

            foreach (var key in Typed("l", "l", "l", "l"))
                keys.OnNext(key);
            Assert.Equal("l x 4 ", TextOf(messages.Last()));

            settings.RepeatedKeyGrouping = RepeatedKeyGrouping.Never;
            keys.OnNext(Typed("l").Single());
            Assert.Equal("lllll", TextOf(messages.Last()));

            settings.RepeatedKeyGrouping = RepeatedKeyGrouping.Threshold;
            settings.RepeatedKeyThreshold = 7;
            keys.OnNext(Typed("l").Single());
            Assert.Equal("llllll", TextOf(messages.Last()));
            keys.OnNext(Typed("l").Single());
            Assert.Equal("l x 7 ", TextOf(messages.Last()));
        }

        [Fact]
        public async Task the_same_shortcut_pressed_again_updates_one_message()
        {
            var sut = CreateMessageProvider(new PopupSettings(), new[] { CtrlDown(), CtrlDown(), CtrlDown() }.ToObservable());

            var messages = await sut.GetMessageStream().ToList();

            Assert.Equal(3, messages.Count);
            Assert.Equal("Ctrl + ↓", TextOf(messages[0]));
            Assert.Equal("Ctrl + ↓ x 2 ", TextOf(messages[1]));
            Assert.Equal("Ctrl + ↓ x 3 ", TextOf(messages[2]));
            Assert.Same(messages[0], messages[1].Previous);
            Assert.Same(messages[1], messages[2].Previous);
        }

        [Fact]
        public async Task the_same_shortcut_is_grouped_when_typed_grouping_is_off()
        {
            var settings = new PopupSettings { RepeatedKeyGrouping = RepeatedKeyGrouping.Never };
            var sut = CreateMessageProvider(settings, new[] { CtrlDown(), CtrlDown(), CtrlDown() }.ToObservable());

            var messages = await sut.GetMessageStream().ToList();

            Assert.Equal("Ctrl + ↓ x 3 ", TextOf(messages.Last()));
        }

        [Fact]
        public async Task different_shortcuts_stay_separate_messages()
        {
            var keys = new[] { Ctrl(Keys.R, "R"), Ctrl(Keys.T, "T"), Ctrl(Keys.T, "T") };
            var sut = CreateMessageProvider(new PopupSettings(), keys.ToObservable());

            var messages = await sut.GetMessageStream().ToList();

            Assert.Equal(3, messages.Count);
            Assert.Equal("Ctrl + R", TextOf(messages[0]));
            Assert.Equal("Ctrl + T", TextOf(messages[1]));
            Assert.Equal("Ctrl + T x 2 ", TextOf(messages[2]));
            Assert.Null(messages[1].Previous);
            Assert.Same(messages[1], messages[2].Previous);
        }

        [Fact]
        public async Task typed_text_after_a_repeated_shortcut_is_a_new_message()
        {
            var keys = new[] { CtrlDown(), CtrlDown() }.Concat(Typed("a"));
            var sut = CreateMessageProvider(new PopupSettings(), keys.ToObservable());

            var messages = await sut.GetMessageStream().ToList();

            Assert.Equal(3, messages.Count);
            Assert.Equal("a", TextOf(messages[2]));
            Assert.Null(messages[2].Previous);
        }

        [Fact]
        public async Task a_repeated_matched_shortcut_is_still_a_shortcut()
        {
            var settings = new PopupSettings { DetectShortcutsOnly = true };
            var sut = CreateMessageProvider(settings, new[] { CtrlDown(), CtrlDown(), CtrlDown() }.ToObservable());
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>())
                .Returns(new List<KeyShortcut> { new KeyShortcut("Scroll down", new KeyPressDefinition(Keys.Down, controlPressed: true)) });

            var messages = await sut.GetMessageStream().ToList();

            Assert.Equal(3, messages.Count);
            Assert.True(messages.All(m => m.IsShortcut));
            Assert.Equal("Ctrl + ↓ x 3 [Scroll down]", TextOf(messages.Last()));
        }

        // The key provider reads the live foreground window, so these tests pin the process instead.
        MessageProvider CreateMessageProvider(PopupSettings settings, IObservable<KeyPress> keys)
        {
            var keyProvider = Substitute.For<IKeyProvider>();
            keyProvider.GetKeyStream().Returns(keys);
            return new MessageProvider(shortcutProvider, keyProvider, settings);
        }

        static readonly ProcessInfo fakeProcess = new ProcessInfo("FakeProcess");

        static KeyPress[] Typed(params string[] texts)
        {
            return texts
                .Select(text => new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.A, KeyDirection.Down, false, false, false), false, new[] { text }))
                .ToArray();
        }

        static KeyPress Ctrl(Keys key, string name)
        {
            return new KeyPress(fakeProcess, new InterceptKeyEventArgs(key, KeyDirection.Down, false, true, false), false, new[] { "Ctrl", name });
        }

        static KeyPress CtrlDown()
        {
            return Ctrl(Keys.Down, "Down");
        }

        static string TextOf(Message message)
        {
            return string.Join(string.Empty, message.Text);
        }
    }
}