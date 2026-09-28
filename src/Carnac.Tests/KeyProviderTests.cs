using System;
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
            Assert.Equal(new[] { "Win", "E" }, processedKeys.Single().Input);
        }

        async Task<string[]> InputOf(KeyPlayer player)
        {
            var provider = new KeyProvider(player, passwordModeService, desktopLockEventService, settingsProvider);

            var processedKeys = await provider.GetKeyStream().ToList();

            return processedKeys.Single().Input.ToArray();
        }

        [Fact]
        public async Task windows_key_with_a_letter_is_shown_as_a_shortcut_with_a_capital_letter()
        {
            Assert.Equal(new[] { "Win", "Q" }, await InputOf(KeyStreams.Combination(Keys.Q, win: true)));
        }

        [Fact]
        public async Task windows_key_with_shift_and_a_letter_keeps_the_shift()
        {
            // Win+Shift+F used to be shown as Win + F
            Assert.Equal(new[] { "Win", "Shift", "F" }, await InputOf(KeyStreams.Combination(Keys.F, win: true, shift: true)));
        }

        [Fact]
        public async Task windows_key_with_shift_and_a_digit_shows_the_key_not_the_symbol()
        {
            Assert.Equal(new[] { "Win", "Shift", "1" }, await InputOf(KeyStreams.Combination(Keys.D1, win: true, shift: true)));
        }

        [Fact]
        public async Task windows_key_with_space_is_a_shortcut()
        {
            // the space is shown as "Space" when the message text is built because a modifier is held
            Assert.Equal(new[] { "Win", " " }, await InputOf(KeyStreams.Combination(Keys.Space, win: true)));
        }

        [Fact]
        public async Task control_shift_and_windows_are_listed_in_the_same_order_as_before()
        {
            Assert.Equal(new[] { "Ctrl", "Alt", "Win", "Shift", "F" },
                await InputOf(KeyStreams.Combination(Keys.F, control: true, alt: true, win: true, shift: true)));
        }

        [Fact]
        public async Task shift_with_a_letter_is_still_a_typed_capital()
        {
            Assert.Equal(new[] { "F" }, await InputOf(KeyStreams.Combination(Keys.F, shift: true)));
        }

        [Fact]
        public async Task shift_with_a_key_that_types_nothing_shows_the_shift()
        {
            Assert.Equal(new[] { "Shift", "Tab" }, await InputOf(KeyStreams.Combination(Keys.Tab, shift: true)));
            Assert.Equal(new[] { "Shift", "F5" }, await InputOf(KeyStreams.Combination(Keys.F5, shift: true)));
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

        // A process name that cannot match the current process: longer than the current name and containing it only as a
        // suffix, so even an unanchored expression built from it never matches the observed name. Tests must not assume
        // which application owns the foreground window (a runner, an IDE, Notepad ...).
        static string OtherProcessName(string suffix = "")
        {
            return "zzz-other-" + CurrentProcessName().ToLowerInvariant() + suffix;
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
            // alternatives that do not name the current process show nothing, listing it as well shows it
            var others = Regex.Escape(OtherProcessName("-1")) + "|" + Regex.Escape(OtherProcessName("-2"));

            Assert.Equal(0, await KeysShownWithFilter(others));
            Assert.Equal(1, await KeysShownWithFilter(others + "|" + Regex.Escape(CurrentProcessName())));
        }

        [Fact]
        public async Task exclusion_filter_hides_the_excluded_process()
        {
            // "^(?!ZoomIt64$)" excludes ZoomIt64; here the excluded name is the process the test runs in
            var excludeCurrentProcess = "^(?!" + Regex.Escape(CurrentProcessName()) + "$)";

            Assert.Equal(0, await KeysShownWithFilter(excludeCurrentProcess));
        }

        [Fact]
        public async Task exclusion_filter_shows_other_processes()
        {
            // excluding some other process leaves the current one visible
            Assert.Equal(1, await KeysShownWithFilter("^(?!" + Regex.Escape(OtherProcessName()) + "$)"));
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
    }
}
