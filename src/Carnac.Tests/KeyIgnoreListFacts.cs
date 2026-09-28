using System.Collections.Generic;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.Models;
using Xunit;

namespace Carnac.Tests
{
    public class KeyIgnoreListFacts
    {
        readonly List<string> warnings = new List<string>();

        static KeyPress Press(Keys key, bool control = false, bool shift = false, bool alt = false, bool win = false)
        {
            return KeyPresses.Create("game", key, control, shift, alt, win);
        }

        KeyIgnoreList CreateSut()
        {
            return new KeyIgnoreList(warnings.Add);
        }

        [Fact]
        public void nothing_is_ignored_by_default()
        {
            var sut = CreateSut();

            Assert.False(sut.IsIgnored(null, Press(Keys.W)));
            Assert.False(sut.IsIgnored("", Press(Keys.W)));
            Assert.False(sut.IsIgnored("  \r\n ", Press(Keys.W)));
            Assert.Empty(warnings);
        }

        [Fact]
        public void listed_keys_are_ignored_and_others_are_not()
        {
            var sut = CreateSut();

            foreach (var key in new[] { Keys.W, Keys.A, Keys.S, Keys.D })
                Assert.True(sut.IsIgnored("W,A,S,D", Press(key)), key.ToString());

            Assert.False(sut.IsIgnored("W,A,S,D", Press(Keys.X)));
            Assert.False(sut.IsIgnored("W,A,S,D", Press(Keys.Q)));
            Assert.Empty(warnings);
        }

        [Fact]
        public void entries_are_independent_keys_not_a_chord()
        {
            var sut = CreateSut();

            // "W,A" ignores W and it ignores A, it does not mean "W then A"
            Assert.True(sut.IsIgnored("W,A", Press(Keys.A)));
        }

        [Fact]
        public void modifiers_must_match_exactly()
        {
            var sut = CreateSut();

            Assert.False(sut.IsIgnored("W", Press(Keys.W, control: true)));
            Assert.False(sut.IsIgnored("W", Press(Keys.W, shift: true)));
            Assert.False(sut.IsIgnored("W", Press(Keys.W, alt: true)));
            Assert.False(sut.IsIgnored("W", Press(Keys.W, win: true)));
            Assert.False(sut.IsIgnored("Ctrl+W", Press(Keys.W)));
            Assert.True(sut.IsIgnored("Ctrl+W", Press(Keys.W, control: true)));
            Assert.False(sut.IsIgnored("Ctrl+W", Press(Keys.W, control: true, shift: true)));
        }

        [Fact]
        public void combinations_with_several_modifiers_are_supported()
        {
            var sut = CreateSut();

            Assert.True(sut.IsIgnored("Ctrl+Alt+Delete", Press(Keys.Delete, control: true, alt: true)));
            Assert.False(sut.IsIgnored("Ctrl+Alt+Delete", Press(Keys.Delete, control: true)));
            Assert.True(sut.IsIgnored("Win+Shift+S", Press(Keys.S, win: true, shift: true)));
            Assert.False(sut.IsIgnored("Win+Shift+S", Press(Keys.S, shift: true)));
        }

        [Fact]
        public void entries_may_be_separated_by_new_lines_and_spaces()
        {
            var sut = CreateSut();
            const string text = "W, A\r\nS\nD\r\n\r\nCtrl+Alt+Delete,";

            foreach (var key in new[] { Keys.W, Keys.A, Keys.S, Keys.D })
                Assert.True(sut.IsIgnored(text, Press(key)), key.ToString());
            Assert.True(sut.IsIgnored(text, Press(Keys.Delete, control: true, alt: true)));
            Assert.Empty(warnings);
        }

        [Fact]
        public void the_comma_key_can_be_ignored_with_a_modifier()
        {
            var sut = CreateSut();

            Assert.True(sut.IsIgnored("Ctrl+,", Press(Keys.Oemcomma, control: true)));
            Assert.True(sut.IsIgnored("Oemcomma", Press(Keys.Oemcomma)));
        }

        [Fact]
        public void the_text_is_case_insensitive()
        {
            var sut = CreateSut();

            Assert.True(sut.IsIgnored("w, ctrl+f5", Press(Keys.W)));
            Assert.True(sut.IsIgnored("w, ctrl+f5", Press(Keys.F5, control: true)));
        }

        [Fact]
        public void a_bad_entry_is_reported_once_and_does_not_disable_the_valid_ones()
        {
            var sut = CreateSut();
            const string text = "W,Bogus,A";

            Assert.True(sut.IsIgnored(text, Press(Keys.W)));
            Assert.True(sut.IsIgnored(text, Press(Keys.A)));
            Assert.False(sut.IsIgnored(text, Press(Keys.B)));
            Assert.True(sut.IsIgnored(text, Press(Keys.W)));

            var warning = Assert.Single(warnings);
            Assert.Contains("Ignored keys", warning);
            Assert.Contains("Bogus", warning);
        }

        [Fact]
        public void a_chord_written_with_a_space_is_reported_not_guessed()
        {
            var sut = CreateSut();

            Assert.False(sut.IsIgnored("Ctrl+K Ctrl+C", Press(Keys.C, control: true)));

            Assert.Single(warnings);
        }

        [Fact]
        public void changes_of_the_text_apply_immediately()
        {
            var sut = CreateSut();

            Assert.False(sut.IsIgnored("A", Press(Keys.W)));
            Assert.True(sut.IsIgnored("W", Press(Keys.W)));
            Assert.False(sut.IsIgnored("", Press(Keys.W)));
            Assert.False(sut.IsIgnored(null, Press(Keys.W)));
        }

        [Fact]
        public void only_the_key_and_its_modifiers_are_compared()
        {
            var sut = CreateSut();

            // a different process or text for the same key must not matter
            Assert.True(sut.IsIgnored("W", KeyPresses.Create("other process", Keys.W)));
        }

        [Fact]
        public void a_key_is_required()
        {
            Assert.Throws<System.ArgumentNullException>(() => CreateSut().IsIgnored("W", null));
        }
    }
}
