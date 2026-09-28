using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.Enums;
using Carnac.Logic.Models;
using Xunit;
using Message = Carnac.Logic.Models.Message;

namespace Carnac.Tests
{
    public class KeyVisibilityFilterFacts
    {
        readonly List<string> warnings = new List<string>();
        readonly PopupSettings settings = new PopupSettings();

        KeyVisibilityFilter CreateSut()
        {
            return new KeyVisibilityFilter(settings, warnings.Add);
        }

        static Message Typed(Keys key, bool shift = false, bool control = false, bool alt = false, bool win = false)
        {
            return new Message(KeyPresses.Create("app", key, control, shift, alt, win));
        }

        static Message NamedShortcut(string name, params KeyPress[] keys)
        {
            return new Message(keys, new KeyShortcut(name), true);
        }

        [Fact]
        public void everything_is_visible_with_the_default_settings()
        {
            var sut = CreateSut();

            Assert.Equal(KeyCategory.All, settings.VisibleKeyCategories);
            foreach (var key in new[] { Keys.A, Keys.D1, Keys.Oemcomma, Keys.Space, Keys.Back, Keys.Up, Keys.F5, Keys.CapsLock })
                Assert.True(sut.IsVisible(Typed(key)), key.ToString());
        }

        [Fact]
        public void only_function_and_navigation_keys_are_shown_when_only_those_are_enabled()
        {
            settings.VisibleKeyCategories = KeyCategory.Function | KeyCategory.Navigation;
            var sut = CreateSut();

            // typing prose
            foreach (var key in new[] { Keys.T, Keys.H, Keys.D1, Keys.Oemcomma, Keys.OemPeriod, Keys.Space, Keys.Enter, Keys.Back })
                Assert.False(sut.IsVisible(Typed(key)), key + " should be hidden");

            Assert.True(sut.IsVisible(Typed(Keys.F5)));
            Assert.True(sut.IsVisible(Typed(Keys.Up)));
            Assert.True(sut.IsVisible(Typed(Keys.PageDown)));
            Assert.True(sut.IsVisible(Typed(Keys.S, control: true)));
        }

        [Fact]
        public void a_category_can_be_switched_off_on_its_own()
        {
            settings.VisibleKeyCategories = KeyCategory.All & ~KeyCategory.Letters;
            var sut = CreateSut();

            Assert.False(sut.IsVisible(Typed(Keys.A)));
            Assert.False(sut.IsVisible(Typed(Keys.A, shift: true)));
            Assert.True(sut.IsVisible(Typed(Keys.D1)));
            Assert.True(sut.IsVisible(Typed(Keys.F5)));
        }

        [Fact]
        public void keys_with_ctrl_alt_or_windows_are_always_shown()
        {
            settings.VisibleKeyCategories = KeyCategory.None;
            var sut = CreateSut();

            Assert.True(sut.IsVisible(Typed(Keys.S, control: true)));
            Assert.True(sut.IsVisible(Typed(Keys.Left, alt: true)));
            Assert.True(sut.IsVisible(Typed(Keys.E, win: true)));
            Assert.True(sut.IsVisible(Typed(Keys.F, win: true, shift: true)));
            Assert.True(sut.IsVisible(Typed(Keys.D1, control: true, shift: true)));
        }

        [Fact]
        public void shift_alone_does_not_make_a_key_visible()
        {
            settings.VisibleKeyCategories = KeyCategory.Navigation;
            var sut = CreateSut();

            Assert.False(sut.IsVisible(Typed(Keys.F5, shift: true)));
            Assert.True(sut.IsVisible(Typed(Keys.Left, shift: true)));
        }

        [Fact]
        public void recognised_keymap_shortcuts_are_shown_whatever_the_categories()
        {
            settings.VisibleKeyCategories = KeyCategory.None;
            var sut = CreateSut();

            // a single letter shortcut such as "T" (Switch Monitor) in kdenlive.yml
            Assert.True(sut.IsVisible(NamedShortcut("Switch Monitor", KeyPresses.Create("kdenlive", Keys.T))));
            Assert.True(sut.IsVisible(NamedShortcut("Comment", KeyPresses.Create("code", Keys.K, control: true), KeyPresses.Create("code", Keys.C, control: true))));
        }

        [Fact]
        public void a_message_without_key_presses_is_visible()
        {
            settings.VisibleKeyCategories = KeyCategory.None;

            Assert.True(CreateSut().IsVisible(new Message()));
        }

        [Fact]
        public void ignored_keys_are_hidden_and_others_are_not()
        {
            settings.IgnoredKeys = "W,A,S,D";
            var sut = CreateSut();

            foreach (var key in new[] { Keys.W, Keys.A, Keys.S, Keys.D })
                Assert.False(sut.IsVisible(Typed(key)), key + " should be ignored");

            foreach (var key in new[] { Keys.Q, Keys.E, Keys.Space, Keys.F5, Keys.Up })
                Assert.True(sut.IsVisible(Typed(key)), key + " should be shown");
            Assert.True(sut.IsVisible(Typed(Keys.W, control: true)));
            Assert.Empty(warnings);
        }

        [Fact]
        public void an_ignored_combination_is_hidden_even_though_it_is_a_shortcut()
        {
            settings.IgnoredKeys = "Ctrl+Alt+Delete, Ctrl+S";
            var sut = CreateSut();

            Assert.False(sut.IsVisible(Typed(Keys.Delete, control: true, alt: true)));
            Assert.False(sut.IsVisible(Typed(Keys.S, control: true)));
            Assert.False(sut.IsVisible(NamedShortcut("Save", KeyPresses.Create("code", Keys.S, control: true))));
            Assert.True(sut.IsVisible(Typed(Keys.S, control: true, shift: true)));
        }

        [Fact]
        public void a_chord_is_hidden_only_when_all_of_its_keys_are_ignored()
        {
            var chord = NamedShortcut("Comment", KeyPresses.Create("code", Keys.K, control: true), KeyPresses.Create("code", Keys.C, control: true));

            settings.IgnoredKeys = "Ctrl+C";
            Assert.True(CreateSut().IsVisible(chord));

            settings.IgnoredKeys = "Ctrl+K,Ctrl+C";
            Assert.False(CreateSut().IsVisible(chord));
        }

        [Fact]
        public void ignoring_wins_over_the_categories()
        {
            settings.VisibleKeyCategories = KeyCategory.Letters;
            settings.IgnoredKeys = "W";
            var sut = CreateSut();

            Assert.False(sut.IsVisible(Typed(Keys.W)));
            Assert.True(sut.IsVisible(Typed(Keys.Q)));
        }

        [Fact]
        public void changes_of_the_settings_apply_to_the_next_message()
        {
            var sut = CreateSut();
            var message = Typed(Keys.W);

            Assert.True(sut.IsVisible(message));
            settings.IgnoredKeys = "W";
            Assert.False(sut.IsVisible(message));
            settings.IgnoredKeys = "";
            settings.VisibleKeyCategories = KeyCategory.Digits;
            Assert.False(sut.IsVisible(message));
            settings.VisibleKeyCategories = KeyCategory.All;
            Assert.True(sut.IsVisible(message));
        }

        [Fact]
        public void a_message_and_settings_are_required()
        {
            Assert.Equal("settings", Assert.Throws<ArgumentNullException>(() => new KeyVisibilityFilter(null)).ParamName);
            Assert.Equal("message", Assert.Throws<ArgumentNullException>(() => CreateSut().IsVisible(null)).ParamName);
        }
    }
}
