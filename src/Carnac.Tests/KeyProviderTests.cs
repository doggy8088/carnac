using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
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
    public class KeyProviderTests
    {
        readonly IPasswordModeService passwordModeService;
        readonly IDesktopLockEventService desktopLockEventService;
        readonly ISettingsProvider settingsProvider;

        public KeyProviderTests()
        {
            passwordModeService = new PasswordModeService();
            desktopLockEventService = Substitute.For<IDesktopLockEventService>();
            desktopLockEventService.GetSessionSwitchStream().Returns(Observable.Never<SessionSwitchEventArgs>());
            settingsProvider = Substitute.For<ISettingsProvider>();
        }

        [Fact]
        public void constructor_requires_settings_provider()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new KeyProvider(KeyStreams.LetterL(), passwordModeService, desktopLockEventService, null));

            Assert.Equal("settingsProvider", exception.ParamName);
        }

        [Fact]
        public async Task ctrlshiftl_is_processed_correctly()
        {
            // arrange
            var player = KeyStreams.CtrlShiftL();
            var provider = new KeyProvider(player, passwordModeService, desktopLockEventService, settingsProvider);

            // act
            var processedKeys = await provider.GetKeyStream().ToList();

            // assert
            Assert.Equal(new[] { "Ctrl", "Shift", "L" }, processedKeys.Single().Input);
        }

        [Fact]
        public async Task shift_is_not_outputted_when_is_being_used_as_a_modifier_key()
        {
            // arrange
            var player = KeyStreams.ShiftL();
            var provider = new KeyProvider(player, passwordModeService, desktopLockEventService, settingsProvider);

            // act
            var processedKeys = await provider.GetKeyStream().ToList();

            // assert

            Assert.Equal(new[] { "L" }, processedKeys.Single().Input);
        }

        [Fact]
        public async Task key_without_shift_is_lowercase()
        {
            // arrange
            var player = KeyStreams.LetterL();
            var provider = new KeyProvider(player, passwordModeService, desktopLockEventService, settingsProvider);

            // act
            var processedKeys = await provider.GetKeyStream().ToList();

            // assert
            Assert.Equal(new[] { "l" }, processedKeys.Single().Input);
        }

        [Fact]
        public async Task verify_number()
        {
            // arrange
            var player = KeyStreams.Number1();
            var provider = new KeyProvider(player, passwordModeService, desktopLockEventService, settingsProvider);

            // act
            var processedKeys = await provider.GetKeyStream().ToList();

            // assert
            Assert.Equal(new[] { "1" }, processedKeys.Single().Input);
        }

        [Fact]
        public async Task verify_shift_number()
        {
            // arrange
            var player = KeyStreams.ExclaimationMark();
            var provider = new KeyProvider(player, passwordModeService, desktopLockEventService, settingsProvider);

            // act
            var processedKeys = await provider.GetKeyStream().ToList();

            // assert
            Assert.Equal(new[] { "!" }, processedKeys.Single().Input);
        }

        [Fact]
        public async Task keyprovider_detects_windows_key_presses()
        {
            // arrange
            var player = KeyStreams.WinkeyE();
            var provider = new KeyProvider(player, passwordModeService, desktopLockEventService, settingsProvider);

            // act
            var processedKeys = await provider.GetKeyStream().ToList();

            // assert
            Assert.Equal(new[] { "Win", "e" }, processedKeys.Single().Input);
        }

        [Fact]
        public async Task output_with_matching_filter()
        {
            // arrange
            string currentProcessName = AssociatedProcessUtilities.GetAssociatedProcess().ProcessName;
            settingsProvider.GetSettings<PopupSettings>().Returns(new PopupSettings() { ProcessFilterExpression = currentProcessName });
            var player = KeyStreams.LetterL();
            var provider = new KeyProvider(player, passwordModeService, desktopLockEventService, settingsProvider);

            // act
            var processedKeys = await provider.GetKeyStream().ToList();

            // assert
            Assert.Equal(new[] { "l" }, processedKeys.Single().Input);
        }

        [Fact]
        public async Task no_output_with_no_match_filter()
        {
            // arrange
            settingsProvider.GetSettings<PopupSettings>().Returns(new PopupSettings() { ProcessFilterExpression = "notepad" });
            var player = KeyStreams.LetterL();
            var provider = new KeyProvider(player, passwordModeService, desktopLockEventService, settingsProvider);

            // act
            var processedKeys = await provider.GetKeyStream().ToList();

            // assert
            Assert.Equal(0, processedKeys.Count);
        }

        async Task<int> KeysShownWithFilter(string processFilterExpression)
        {
            settingsProvider.GetSettings<PopupSettings>().Returns(new PopupSettings() { ProcessFilterExpression = processFilterExpression });
            var provider = new KeyProvider(KeyStreams.LetterL(), passwordModeService, desktopLockEventService, settingsProvider);

            var processedKeys = await provider.GetKeyStream().ToList();

            return processedKeys.Count;
        }

        static string CurrentProcessName()
        {
            return AssociatedProcessUtilities.GetAssociatedProcess().ProcessName;
        }

        [Fact]
        public async Task filter_is_matched_case_insensitively()
        {
            Assert.Equal(1, await KeysShownWithFilter(Regex.Escape(CurrentProcessName().ToUpperInvariant())));
            Assert.Equal(1, await KeysShownWithFilter(Regex.Escape(CurrentProcessName().ToLowerInvariant())));
        }

        [Fact]
        public async Task filter_with_alternatives_shows_only_the_listed_processes()
        {
            // "notepad|calc": only these applications
            Assert.Equal(0, await KeysShownWithFilter("notepad|calc"));
            Assert.Equal(1, await KeysShownWithFilter("notepad|calc|" + Regex.Escape(CurrentProcessName())));
        }

        [Fact]
        public async Task exclusion_filter_hides_the_excluded_process()
        {
            // "^(?!ZoomIt64$)": everything except ZoomIt64, here with the process the test runs in
            var excludeCurrentProcess = "^(?!" + Regex.Escape(CurrentProcessName()) + "$)";

            Assert.Equal(0, await KeysShownWithFilter(excludeCurrentProcess));
        }

        [Fact]
        public async Task exclusion_filter_shows_other_processes()
        {
            Assert.Equal(1, await KeysShownWithFilter("^(?!ZoomIt64$)"));
        }

        [Fact]
        public async Task exclusion_filter_with_exe_suffix_excludes_nothing()
        {
            // Process names have no ".exe", so the negative look-ahead of "^(?!<name>\.exe$)" always succeeds.
            var withExe = "^(?!" + Regex.Escape(CurrentProcessName()) + @"\.exe$)";

            Assert.Equal(1, await KeysShownWithFilter(withExe));
        }

        [Fact]
        public async Task no_output_with_no_match_filter_for_multiple_keypresses()
        {
            // arrange
            settingsProvider.GetSettings<PopupSettings>().Returns(new PopupSettings() { ProcessFilterExpression = "notepad" });
            var player = new KeyPlayer
                         {
                             new InterceptKeyEventArgs(Keys.L, KeyDirection.Down, false, false, false),
                             new InterceptKeyEventArgs(Keys.L, KeyDirection.Up, false, false, false),
                             new InterceptKeyEventArgs(Keys.U, KeyDirection.Down, false, false, false),
                             new InterceptKeyEventArgs(Keys.U, KeyDirection.Up, false, false, false),
                         };
            var provider = new KeyProvider(player, passwordModeService, desktopLockEventService, settingsProvider);

            // act
            var processedKeys = await provider.GetKeyStream().ToList();

            // assert
            Assert.Equal(0, processedKeys.Count);
        }

        [Fact]
        public async Task typed_digits_and_operators_are_shown_as_typed_when_repeated()
        {
            // arrange
            var player = new KeyPlayer();
            foreach (var key in new[] { Keys.D1, Keys.D0, Keys.D0, Keys.D0, Keys.D0, Keys.Add, Keys.Add, Keys.NumPad5, Keys.NumPad5 })
            {
                player.Add(new InterceptKeyEventArgs(key, KeyDirection.Down, false, false, false));
                player.Add(new InterceptKeyEventArgs(key, KeyDirection.Up, false, false, false));
            }
            var provider = new KeyProvider(player, passwordModeService, desktopLockEventService, settingsProvider);

            // act
            var processedKeys = await provider.GetKeyStream().ToList();
            var message = MergeIntoOneMessage(processedKeys);

            // assert
            Assert.Equal("10000 +  + 55", string.Join(string.Empty, message.Text));
        }

        [Fact]
        public async Task shifted_symbols_are_shown_as_typed_when_repeated_and_summarised_from_four()
        {
            // arrange
            var player = new KeyPlayer();
            for (var i = 0; i < 5; i++)
            {
                player.Add(new InterceptKeyEventArgs(Keys.D1, KeyDirection.Down, false, false, true));
                player.Add(new InterceptKeyEventArgs(Keys.D1, KeyDirection.Up, false, false, true));
            }
            var provider = new KeyProvider(player, passwordModeService, desktopLockEventService, settingsProvider);

            // act
            var processedKeys = await provider.GetKeyStream().ToList();
            var first3 = MergeIntoOneMessage(processedKeys.Take(3));
            var all = MergeIntoOneMessage(processedKeys);

            // assert
            Assert.Equal("!!!", string.Join(string.Empty, first3.Text));
            Assert.Equal("! x 5 ", string.Join(string.Empty, all.Text));
        }

        // The provider reads the live foreground window, so pin the process to keep the messages mergeable
        // whatever has the focus while the tests run.
        static Message MergeIntoOneMessage(IEnumerable<KeyPress> keyPresses)
        {
            // no key press at all means there is no foreground window to take the process from
            Assert.NotEmpty(keyPresses);

            var process = new ProcessInfo("FakeProcess");
            return keyPresses
                .Select(k => new Message(new KeyPress(process, k.InterceptKeyEventArgs, false, k.Input)))
                .Aggregate((merged, next) => merged.Merge(next));
        }
    }
}
