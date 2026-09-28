using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Message = Carnac.Logic.Models.Message;
using Microsoft.Reactive.Testing;
using Microsoft.Win32;
using NSubstitute;
using SettingsProviderNet;
using Xunit;

namespace Carnac.Tests
{
    public class MessageProviderFacts
    {
        readonly IShortcutProvider shortcutProvider;
        readonly TestScheduler testScheduler = new TestScheduler();

        public MessageProviderFacts()
        {
            shortcutProvider = Substitute.For<IShortcutProvider>();
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>()).Returns(new List<KeyShortcut>());
            
        }

        MessageProvider CreateMessageProvider(IObservable<InterceptKeyEventArgs> keysStreamSource, PopupSettings popupSettings = null)
        {
            var source = Substitute.For<IInterceptKeys>();
            source.GetKeyStream().Returns(keysStreamSource);
            var desktopLockEventService = Substitute.For<IDesktopLockEventService>();
            var settingsProvider = Substitute.For<ISettingsProvider>();
            desktopLockEventService.GetSessionSwitchStream().Returns(Observable.Never<SessionSwitchEventArgs>());
            var keyProvider = new KeyProvider(source, new PasswordModeService(), desktopLockEventService, settingsProvider);
            var concurrencyService = Substitute.For<IConcurrencyService>();
            concurrencyService.MainThreadScheduler.Returns(testScheduler);
            concurrencyService.Default.Returns(testScheduler);
            return new MessageProvider(shortcutProvider, keyProvider, popupSettings ?? new PopupSettings(), concurrencyService);
        }

        async Task<IList<Message>> MessagesFor(KeyPlayer keys, PopupSettings popupSettings)
        {
            return await CreateMessageProvider(keys.ToObservable(), popupSettings).GetMessageStream().ToList();
        }

        [Fact]
        public async Task only_modifiers_filter_shows_shift_enter()
        {
            var messages = await MessagesFor(KeyStreams.Combination(Keys.Enter, shift: true), new PopupSettings { ShowOnlyModifiers = true });

            var text = string.Join("", messages.Single().Text);
            Assert.True(text.StartsWith("Shift + ", StringComparison.Ordinal), text);
        }

        [Fact]
        public async Task only_modifiers_filter_shows_shift_with_tab_function_and_arrow_keys()
        {
            foreach (var key in new[] { Keys.Tab, Keys.F5, Keys.Up, Keys.PageDown })
            {
                var messages = await MessagesFor(KeyStreams.Combination(key, shift: true), new PopupSettings { ShowOnlyModifiers = true });

                Assert.True(messages.Count == 1, "Shift+" + key + " should pass the modifier filter");
            }
        }

        [Fact]
        public async Task only_modifiers_filter_hides_plain_letters_and_typed_capitals()
        {
            var settings = new PopupSettings { ShowOnlyModifiers = true };

            Assert.Empty(await MessagesFor(KeyStreams.LetterL(), settings));
            Assert.Empty(await MessagesFor(KeyStreams.ShiftL(), settings));
            Assert.Empty(await MessagesFor(KeyStreams.ExclaimationMark(), settings));
            Assert.Empty(await MessagesFor(KeyStreams.Combination(Keys.Enter), settings));
        }

        [Fact]
        public async Task only_modifiers_filter_still_shows_ctrl_alt_and_windows_combinations()
        {
            var settings = new PopupSettings { ShowOnlyModifiers = true };

            Assert.Equal(1, (await MessagesFor(KeyStreams.CtrlU(), settings)).Count);
            Assert.Equal(1, (await MessagesFor(KeyStreams.Combination(Keys.Left, alt: true), settings)).Count);
            Assert.Equal(1, (await MessagesFor(KeyStreams.Combination(Keys.F, win: true, shift: true), settings)).Count);
        }

        [Fact]
        public async Task shift_enter_shortcut_passes_shortcuts_only_together_with_the_modifier_filter()
        {
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>())
                .Returns(new List<KeyShortcut> { new KeyShortcut("New line", new KeyPressDefinition(Keys.Enter, shiftPressed: true)) });

            var messages = await MessagesFor(KeyStreams.Combination(Keys.Enter, shift: true),
                new PopupSettings { ShowOnlyModifiers = true, DetectShortcutsOnly = true });

            Assert.Equal("New line", messages.Single().ShortcutName);
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
            Assert.Equal("Ctrl + U, l", string.Join("", messages[0].Text));
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
            Assert.Equal("Ctrl + U, l", string.Join("", messages[0].Text));
            Assert.Equal("SomeShortcut", messages[0].ShortcutName);
            Assert.Equal("1", string.Join("", messages[1].Text));
            Assert.Equal("1l", string.Join("", messages[2].Text));
        }
    }
}