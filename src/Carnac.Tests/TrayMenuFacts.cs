using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    public class TrayMenuFacts
    {
        static readonly string Dash = " " + (char)0x2013 + " ";
        static readonly string SettingsCaption = "Settings" + (char)0x2026;

        readonly KeyDisplayState displayState = new KeyDisplayState();
        int settingsRequested;
        int exitRequested;

        TrayMenu CreateMenu(Action<Action> invokeOnUiThread = null)
        {
            return new TrayMenu(displayState, () => settingsRequested++, () => exitRequested++, invokeOnUiThread ?? (action => action()));
        }

        static MenuItem[] ItemsOf(TrayMenu menu)
        {
            return menu.ContextMenu.MenuItems.Cast<MenuItem>().ToArray();
        }

        [Fact]
        public void offers_settings_pause_silent_mode_a_separator_and_exit_in_that_order()
        {
            using (var sut = CreateMenu())
            {
                var texts = ItemsOf(sut).Select(item => item.Text).ToArray();

                Assert.Equal(new[] { SettingsCaption, "Pause", "Silent mode", "-", "Exit" }, texts);
            }
        }

        [Fact]
        public void settings_is_the_default_item()
        {
            using (var sut = CreateMenu())
            {
                var defaults = ItemsOf(sut).Where(item => item.DefaultItem).ToArray();

                Assert.Equal(1, defaults.Length);
                Assert.Equal(SettingsCaption, defaults[0].Text);
            }
        }

        [Fact]
        public void nothing_is_checked_initially()
        {
            using (var sut = CreateMenu())
            {
                Assert.True(ItemsOf(sut).All(item => !item.Checked));
            }
        }

        [Fact]
        public void settings_item_opens_the_settings()
        {
            using (var sut = CreateMenu())
            {
                ItemsOf(sut)[0].PerformClick();

                Assert.Equal(1, settingsRequested);
                Assert.Equal(0, exitRequested);
            }
        }

        [Fact]
        public void exit_item_exits()
        {
            using (var sut = CreateMenu())
            {
                ItemsOf(sut)[4].PerformClick();

                Assert.Equal(1, exitRequested);
                Assert.Equal(0, settingsRequested);
            }
        }

        [Fact]
        public void pause_item_pauses_and_resumes_the_shared_state()
        {
            using (var sut = CreateMenu())
            {
                var pause = ItemsOf(sut)[1];

                pause.PerformClick();
                Assert.True(displayState.IsPaused);
                Assert.True(pause.Checked);
                Assert.Equal("Resume", pause.Text);

                pause.PerformClick();
                Assert.False(displayState.IsPaused);
                Assert.False(pause.Checked);
                Assert.Equal("Pause", pause.Text);
            }
        }

        [Fact]
        public void silent_mode_item_toggles_the_shared_state()
        {
            using (var sut = CreateMenu())
            {
                var silent = ItemsOf(sut)[2];

                silent.PerformClick();
                Assert.True(displayState.IsSilent);
                Assert.True(silent.Checked);

                silent.PerformClick();
                Assert.False(displayState.IsSilent);
                Assert.False(silent.Checked);
            }
        }

        [Fact]
        public void items_follow_state_changes_that_do_not_come_from_the_menu()
        {
            using (var sut = CreateMenu())
            {
                displayState.SetPaused(true);
                displayState.SetSilent(true);

                var items = ItemsOf(sut);
                Assert.True(items[1].Checked);
                Assert.Equal("Resume", items[1].Text);
                Assert.True(items[2].Checked);

                displayState.SetPaused(false);
                displayState.SetSilent(false);

                Assert.False(items[1].Checked);
                Assert.Equal("Pause", items[1].Text);
                Assert.False(items[2].Checked);
            }
        }

        [Fact]
        public void status_text_is_just_the_app_name_initially()
        {
            using (var sut = CreateMenu())
            {
                Assert.Equal("Carnac", sut.StatusText);
            }
        }

        [Fact]
        public void status_text_reflects_pause_and_silent_mode()
        {
            using (var sut = CreateMenu())
            {
                displayState.SetPaused(true);
                Assert.Equal("Carnac" + Dash + "paused", sut.StatusText);

                displayState.SetSilent(true);
                Assert.Equal("Carnac" + Dash + "paused, silent mode", sut.StatusText);

                displayState.SetPaused(false);
                Assert.Equal("Carnac" + Dash + "silent mode", sut.StatusText);

                displayState.SetSilent(false);
                Assert.Equal("Carnac", sut.StatusText);
            }
        }

        [Fact]
        public void status_text_never_exceeds_the_notify_icon_limit()
        {
            using (var sut = CreateMenu())
            {
                displayState.SetPaused(true);
                displayState.SetSilent(true);

                Assert.True(sut.StatusText.Length <= TrayStatusText.MaxLength);
            }
        }

        [Fact]
        public void raises_status_changed_when_the_state_changes()
        {
            using (var sut = CreateMenu())
            {
                var texts = new List<string>();
                sut.StatusChanged += (sender, args) => texts.Add(sut.StatusText);

                displayState.SetPaused(true);

                Assert.Equal(new[] { "Carnac" + Dash + "paused" }, texts.ToArray());
            }
        }

        [Fact]
        public void state_changes_are_applied_through_the_ui_thread_invoker()
        {
            var pending = new List<Action>();
            using (var sut = CreateMenu(pending.Add))
            {
                displayState.SetPaused(true);

                // e.g. the hotkey changed the state on the keyboard hook's thread: nothing is touched until the UI thread runs it
                Assert.Equal(1, pending.Count);
                Assert.False(ItemsOf(sut)[1].Checked);
                Assert.Equal("Carnac", sut.StatusText);

                pending[0]();

                Assert.True(ItemsOf(sut)[1].Checked);
                Assert.Equal("Carnac" + Dash + "paused", sut.StatusText);
            }
        }

        [Fact]
        public void stops_listening_once_disposed()
        {
            var invocations = 0;
            var sut = CreateMenu(action => { invocations++; action(); });

            sut.Dispose();
            displayState.SetPaused(true);

            Assert.Equal(0, invocations);
        }

        [Fact]
        public void constructor_validates_its_arguments()
        {
            Assert.Throws<ArgumentNullException>(() => new TrayMenu(null, () => { }, () => { }, action => action()));
            Assert.Throws<ArgumentNullException>(() => new TrayMenu(displayState, null, () => { }, action => action()));
            Assert.Throws<ArgumentNullException>(() => new TrayMenu(displayState, () => { }, null, action => action()));
            Assert.Throws<ArgumentNullException>(() => new TrayMenu(displayState, () => { }, () => { }, null));
        }
    }
}
