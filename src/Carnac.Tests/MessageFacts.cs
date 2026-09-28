using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.Enums;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Xunit;
using Message = Carnac.Logic.Models.Message;

namespace Carnac.Tests
{
    public class MessageFacts
    {
        readonly ProcessInfo fakeProcess = new ProcessInfo("FakeProcess");

        [Fact]
        public void message_does_not_group_different_letters()
        {
            // arrange
            var message = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Back, KeyDirection.Down, false, false, false), false, new[] { "a" }));

            // act
            var result = message.Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Back, KeyDirection.Down, false, false, false), false, new[] { "b" })));

            // assert
            var expected = string.Join(string.Empty, result.Text);
            Assert.Equal("ab", expected);
        }

        [Fact]
        public void message_does_not_group_letter_and_backspace()
        {
            // arrange
            var message = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Back, KeyDirection.Down, false, false, false), false, new[] { "a" }));

            // act
            var result = message.Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Back, KeyDirection.Down, false, false, false), false, new[] { "Back" })));

            // assert
            Assert.Equal("aBack", string.Join(string.Empty, result.Text));
        }

        [Fact]
        public void message_groups_multiple_backspace_key_presses_together()
        {
            // arrange
            var message = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Back, KeyDirection.Down, false, false, false), false, new[] { "Back" }));

            // act
            var result = message.Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Back, KeyDirection.Down, false, false, false), false, new[] { "Back" })));

            // assert
            Assert.Equal("Back x 2 ", string.Join(string.Empty, result.Text));
        }

        [Fact]
        public void message_does_not_group_different_arrow_key_presses_together()
        {
            // arrange
            var message = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Up, KeyDirection.Down, false, false, false), false, new[] { "Up" }));

            // act
            var result = message.Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, false, false), false, new[] { "Down" })));

            // assert
            Assert.Equal("↑↓", string.Join(string.Empty, result.Text));
        }
        
        [Fact]
        public void message_groups_two_equal_arrow_key_presses_together()
        {
            // arrange
            var message = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, false, false), false, new[] { "Down" }));

            // act
            var result = message.Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, false, false), false, new[] { "Down" })));

            // assert
            Assert.Equal("↓ x 2 ", string.Join(string.Empty, result.Text));
        }

        [Fact]
        public void message_groups_three_equal_arrow_key_presses_together()
        {
            // arrange
            var result = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, false, false), false, new[] { "Down" }))
                .Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, false, false), false, new[] { "Down" })))
                .Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, false, false), false, new[] { "Down" })));

            // assert
            Assert.Equal("↓ x 3 ", string.Join(string.Empty, result.Text));
        }

        static Message MessageFor(Keys key, bool shift = false, bool control = false, bool alt = false)
        {
            return new Message(KeyPresses.Create("FakeProcess", key, control, shift, alt));
        }

        [Fact]
        public void shift_with_a_key_that_types_nothing_is_a_modifier_message_that_is_not_merged()
        {
            foreach (var key in new[] { Keys.Enter, Keys.Tab, Keys.F5, Keys.Left, Keys.Home, Keys.Delete, Keys.Insert })
            {
                var message = MessageFor(key, shift: true);

                Assert.True(message.IsModifier, "Shift+" + key + " should count as a modifier message");
                Assert.False(message.CanBeMerged, "Shift+" + key + " should not be merged into typed text");
            }
        }

        [Fact]
        public void shift_with_a_letter_or_digit_is_typing_and_not_a_modifier_message()
        {
            foreach (var key in new[] { Keys.A, Keys.D1, Keys.Oemcomma, Keys.Space })
            {
                var message = MessageFor(key, shift: true);

                Assert.False(message.IsModifier, "Shift+" + key + " is typing");
                Assert.True(message.CanBeMerged, "Shift+" + key + " is typing");
            }
        }

        [Fact]
        public void plain_enter_and_plain_letters_are_not_modifier_messages()
        {
            Assert.False(MessageFor(Keys.Enter).IsModifier);
            Assert.False(MessageFor(Keys.A).IsModifier);
        }

        [Fact]
        public void ctrl_alt_and_windows_are_still_modifier_messages()
        {
            Assert.True(MessageFor(Keys.S, control: true).IsModifier);
            Assert.True(MessageFor(Keys.Left, alt: true).IsModifier);
            Assert.True(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.E, KeyDirection.Down, false, false, false), true, new[] { "Win", "E" })).IsModifier);
        }

        [Fact]
        public void shift_tab_is_shown_as_its_own_message_not_merged_into_typed_text()
        {
            var typed = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.A, KeyDirection.Down, false, false, false), false, new[] { "a" }));

            var result = Message.MergeIfNeeded(typed, MessageFor(Keys.Tab, shift: true));

            Assert.Equal("Shift + Tab", string.Join(string.Empty, result.Text));
        }

        [Fact]
        public void multiple_shortcuts_have_comma_inserted_between_input()
        {
            // arrange
            var message = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.R, KeyDirection.Down, false, true, false), false, new[] { "Control", "R" }));

            // act
            var result = message.Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.T, KeyDirection.Down, false, true, false), false, new[] { "Control", "T" })));

            // assert
            Assert.Equal("Control + R, Control + T", string.Join(string.Empty, result.Text));
        }
        
        [Fact]
        public void multiple_shortcuts_duplicate_shortcuts_are_grouped()
        {
            // arrange
            var message = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.R, KeyDirection.Down, false, true, false), false, new[] { "Control", "R" }));

            // act
            var result = message.Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.R, KeyDirection.Down, false, true, false), false, new[] { "Control", "R" })));

            // assert
            string actual = string.Join(string.Empty, result.Text);

            Assert.Equal("Control + R x 2 ", actual);
        }

        [Fact]
        public void typed_letters_repeated_twice_are_not_summarised()
        {
            Assert.Equal("hello", TextOf(Typed("h", "e", "l", "l", "o")));
            Assert.Equal("AA", TextOf(Typed("A", "A")));
        }

        [Fact]
        public void typed_letters_repeated_three_times_are_not_summarised()
        {
            Assert.Equal("www", TextOf(Typed("w", "w", "w")));
        }

        [Fact]
        public void typed_letters_repeated_four_times_are_summarised()
        {
            Assert.Equal("l x 4 ", TextOf(Typed("l", "l", "l", "l")));
        }

        [Fact]
        public void summarised_typed_letters_keep_counting()
        {
            Assert.Equal("l x 6 ", TextOf(Typed("l", "l", "l", "l", "l", "l")));
        }

        [Fact]
        public void summarised_typed_letters_are_followed_by_the_next_key()
        {
            Assert.Equal("a x 4 b", TextOf(Typed("a", "a", "a", "a", "b")));
            Assert.Equal("a x 4 bbb", TextOf(Typed("a", "a", "a", "a", "b", "b", "b")));
        }

        [Fact]
        public void typed_full_stops_follow_the_same_rule()
        {
            Assert.Equal("...", TextOf(Typed(".", ".", ".")));
            Assert.Equal(". x 4 ", TextOf(Typed(".", ".", ".", ".")));
        }

        [Fact]
        public void typed_digits_are_only_summarised_from_ten_in_a_row()
        {
            Assert.Equal("1000", TextOf(Typed("1", "0", "0", "0")));
            Assert.Equal("1000000", TextOf(Typed("1", "0", "0", "0", "0", "0", "0")));
            Assert.Equal("1000000000", TextOf(Typed("1", "0", "0", "0", "0", "0", "0", "0", "0", "0")));
            Assert.Equal("0 x 10 ", TextOf(Typed("0", "0", "0", "0", "0", "0", "0", "0", "0", "0")));
        }

        [Fact]
        public void typed_punctuation_and_symbols_follow_the_same_rule()
        {
            Assert.Equal("foo((x))", TextOf(Typed("f", "o", "o", "(", "(", "x", ")", ")")));
            Assert.Equal("a == b", TextOf(Typed("a", " ", "=", "=", " ", "b")));
            Assert.Equal("===", TextOf(Typed("=", "=", "=")));
            Assert.Equal("= x 4 ", TextOf(Typed("=", "=", "=", "=")));
            Assert.Equal("&&", TextOf(Typed("&", "&")));
        }

        [Fact]
        public void typed_uppercase_letters_follow_the_same_rule()
        {
            Assert.Equal("AAA", TextOf(Typed("A", "A", "A")));
            Assert.Equal("A x 4 ", TextOf(Typed("A", "A", "A", "A")));
        }

        [Fact]
        public void typed_characters_outside_ascii_follow_the_same_rule()
        {
            var surrogatePairLetter = "\U0001D49C";

            Assert.Equal("\u00fc\u00fc", TextOf(Typed("\u00fc", "\u00fc")));
            Assert.Equal(surrogatePairLetter + surrogatePairLetter, TextOf(Typed(surrogatePairLetter, surrogatePairLetter)));
            Assert.Equal(surrogatePairLetter + " x 4 ", TextOf(Typed(surrogatePairLetter, surrogatePairLetter, surrogatePairLetter, surrogatePairLetter)));
        }

        [Fact]
        public void named_keys_are_still_summarised_from_two()
        {
            Assert.Equal("Return x 2 ", TextOf(Typed("Return", "Return")));
            Assert.Equal("Tab x 2 ", TextOf(Typed("Tab", "Tab")));
            Assert.Equal("F5 x 2 ", TextOf(Typed("F5", "F5")));
            Assert.Equal("del x 2 ", TextOf(Typed("del", "del")));
        }

        [Fact]
        public void typed_spaces_follow_the_same_rule()
        {
            Assert.Equal("end.  Next", TextOf(Typed("e", "n", "d", ".", " ", " ", "N", "e", "x", "t")));
            Assert.Equal("   ", TextOf(Typed(" ", " ", " ")));
            Assert.Equal("  x 4 ", TextOf(Typed(" ", " ", " ", " ")));
        }

        [Fact]
        public void typed_numpad_operators_follow_the_same_rule()
        {
            // the numpad operators are padded with spaces to read well in a sentence
            Assert.Equal(" +  + ", TextOf(Typed(" + ", " + ")));
            Assert.Equal(" + " + " x 4 ", TextOf(Typed(" + ", " + ", " + ", " + ")));
        }

        [Fact]
        public void a_short_run_before_a_long_run_of_the_same_character_is_counted_separately()
        {
            Assert.Equal("aaba x 4 ", TextOf(Typed("a", "a", "b", "a", "a", "a", "a")));
        }

        [Fact]
        public void repeated_typed_letters_after_a_shortcut_keep_their_separator()
        {
            var shortcut = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.R, KeyDirection.Down, false, true, false), false, new[] { "Control", "R" }));

            var result = shortcut.Merge(Typed("l", "l"));

            Assert.Equal("Control + R, ll", TextOf(result));
        }

        [Fact]
        public void repeated_letters_with_a_modifier_are_still_summarised()
        {
            var press = new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.L, KeyDirection.Down, false, true, false), false, new[] { "Control", "L" });

            var result = new Message(press).Merge(new Message(press));

            Assert.Equal("Control + L x 2 ", TextOf(result));
        }

        [Fact]
        public void a_character_typed_with_altgr_is_a_typed_character()
        {
            // AltGr is reported as Ctrl+Alt, the character is what is typed
            var press = new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.D7, KeyDirection.Down, true, true, false), false, new[] { "{" });

            var result = new Message(press).Merge(new Message(press)).Merge(new Message(press));

            Assert.Equal("{{{", TextOf(result));
        }

        [Fact]
        public void a_run_does_not_mix_typed_characters_with_the_same_text_pressed_with_a_modifier()
        {
            var typed = new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.L, KeyDirection.Down, false, false, false), false, new[] { "l" });
            var withControl = new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.L, KeyDirection.Down, false, true, false), false, new[] { "l" });

            var typedFirst = new Message(typed).Merge(new Message(typed)).Merge(new Message(typed)).Merge(new Message(withControl));
            var controlFirst = new Message(withControl).Merge(new Message(typed)).Merge(new Message(typed)).Merge(new Message(typed));

            // the Ctrl press is a group of its own, so it neither joins nor changes how the typed run is shown
            Assert.Equal("llll", TextOf(typedFirst));
            Assert.Equal("l, lll", TextOf(controlFirst));
        }

        [Fact]
        public void the_default_policy_summarises_typed_characters_from_the_fourth_repeat()
        {
            Assert.Equal("lll", TextOf(Typed(RepeatedKeyPolicy.Default, "l", "l", "l")));
            Assert.Equal("l x 4 ", TextOf(Typed(RepeatedKeyPolicy.Default, "l", "l", "l", "l")));
        }

        [Fact]
        public void typed_characters_are_never_summarised_when_grouping_is_off()
        {
            var never = RepeatedKeyPolicy.Never;

            Assert.Equal("look", TextOf(Typed(never, "l", "o", "o", "k")));
            Assert.Equal("llll", TextOf(Typed(never, "l", "l", "l", "l")));
            Assert.Equal("llllllllll", TextOf(Typed(never, Repeat("l", 10))));
            Assert.Equal("....", TextOf(Typed(never, ".", ".", ".", ".")));
            Assert.Equal("    ", TextOf(Typed(never, " ", " ", " ", " ")));
            Assert.Equal(" +  +  +  + ", TextOf(Typed(never, " + ", " + ", " + ", " + ")));
        }

        [Fact]
        public void digits_are_shown_as_typed_however_long_the_run_when_grouping_is_off()
        {
            Assert.Equal("0000000000", TextOf(Typed(RepeatedKeyPolicy.Never, Repeat("0", 10))));
            Assert.Equal("000000000000", TextOf(Typed(RepeatedKeyPolicy.Never, Repeat("0", 12))));
        }

        [Fact]
        public void named_keys_are_still_summarised_when_grouping_is_off()
        {
            var never = RepeatedKeyPolicy.Never;

            Assert.Equal("↑ x 3 ", TextOf(Typed(never, "Up", "Up", "Up")));
            Assert.Equal("Back x 5 ", TextOf(Typed(never, Repeat("Back", 5))));
            Assert.Equal("Return x 2 ", TextOf(Typed(never, "Return", "Return")));
        }

        [Fact]
        public void named_keys_are_summarised_from_two_whatever_the_threshold()
        {
            var policies = new[]
            {
                RepeatedKeyPolicy.Default,
                RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, 2),
                RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, 10)
            };

            foreach (var policy in policies)
            {
                Assert.Equal("↑ x 2 ", TextOf(Typed(policy, "Up", "Up")));
                Assert.Equal("Back x 5 ", TextOf(Typed(policy, Repeat("Back", 5))));
            }
        }

        [Fact]
        public void a_lower_threshold_summarises_typed_characters_sooner()
        {
            var twice = RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, 2);
            var threeTimes = RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, 3);

            Assert.Equal("hel x 2 o", TextOf(Typed(twice, "h", "e", "l", "l", "o")));
            Assert.Equal("w x 3 ", TextOf(Typed(twice, "w", "w", "w")));
            Assert.Equal("hello", TextOf(Typed(threeTimes, "h", "e", "l", "l", "o")));
            Assert.Equal("w x 3 ", TextOf(Typed(threeTimes, "w", "w", "w")));
        }

        [Fact]
        public void a_higher_threshold_summarises_typed_characters_later()
        {
            var tenTimes = RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, 10);

            Assert.Equal("aaaaaaaaa", TextOf(Typed(tenTimes, Repeat("a", 9))));
            Assert.Equal("a x 10 ", TextOf(Typed(tenTimes, Repeat("a", 10))));
        }

        [Fact]
        public void digits_wait_for_ten_repeats_whatever_the_threshold()
        {
            var twice = RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, 2);

            Assert.Equal("1000", TextOf(Typed(twice, "1", "0", "0", "0")));
            Assert.Equal("000000000", TextOf(Typed(twice, Repeat("0", 9))));
            Assert.Equal("0 x 10 ", TextOf(Typed(twice, Repeat("0", 10))));
        }

        [Fact]
        public void a_typed_character_pressed_with_a_modifier_is_still_summarised_when_grouping_is_off()
        {
            var press = new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.L, KeyDirection.Down, false, true, false), false, new[] { "l" });

            var result = MergeAll(RepeatedKeyPolicy.Never, new Message(press), new Message(press));

            Assert.Equal("l x 2 ", TextOf(result));
        }

        [Fact]
        public void a_merged_message_keeps_the_policy_it_was_merged_with()
        {
            var merged = Typed(RepeatedKeyPolicy.Never, "l", "l", "l", "l");

            Assert.Equal("lllll", TextOf(merged.Merge(new Message(Key("l")))));
            Assert.Equal("llll", TextOf(merged.FadeOut()));
        }

        [Fact]
        public void a_faded_out_message_reads_the_same_with_a_custom_threshold()
        {
            var merged = Typed(RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, 2), "l", "l", "o");

            Assert.Equal("l x 2 o", TextOf(merged));
            Assert.Equal("l x 2 o", TextOf(merged.FadeOut()));
        }

        [Fact]
        public void a_changed_policy_rewrites_the_whole_message_on_the_next_key()
        {
            var summarised = Typed(RepeatedKeyPolicy.Default, "l", "l", "l", "l");
            Assert.Equal("l x 4 ", TextOf(summarised));

            var asTyped = Message.MergeIfNeeded(summarised, new Message(Key("l")), RepeatedKeyPolicy.Never);
            Assert.Equal("lllll", TextOf(asTyped));

            var summarisedAgain = Message.MergeIfNeeded(asTyped, new Message(Key("l")), RepeatedKeyPolicy.Default);
            Assert.Equal("l x 6 ", TextOf(summarisedAgain));
        }

        [Fact]
        public void merging_requires_a_policy()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                Message.MergeIfNeeded(new Message(Key("a")), new Message(Key("b")), null));

            Assert.Equal("repeatPolicy", exception.ParamName);
        }

        [Fact]
        public void the_same_shortcut_pressed_again_is_one_message_with_a_count()
        {
            foreach (var policy in new[] { RepeatedKeyPolicy.Default, RepeatedKeyPolicy.Never })
            {
                var twice = MergeAll(policy, new Message(CtrlDown()), new Message(CtrlDown()));
                var threeTimes = Message.MergeIfNeeded(twice, new Message(CtrlDown()), policy);

                Assert.Equal("Ctrl + ↓ x 2 ", TextOf(twice));
                Assert.Equal("Ctrl + ↓ x 3 ", TextOf(threeTimes));
                Assert.Same(twice, threeTimes.Previous);
                Assert.False(threeTimes.CanBeMerged);
            }
        }

        [Fact]
        public void a_different_shortcut_is_a_new_message()
        {
            var ctrlR = new Message(Ctrl(Keys.R, "R"));
            var ctrlT = new Message(Ctrl(Keys.T, "T"));

            Assert.Same(ctrlT, Message.MergeIfNeeded(ctrlR, ctrlT, RepeatedKeyPolicy.Default));
            Assert.Null(ctrlT.Previous);
        }

        [Fact]
        public void a_shortcut_repeated_after_another_shortcut_is_grouped_on_its_own()
        {
            var ctrlR = new Message(Ctrl(Keys.R, "R"));
            var ctrlT = Message.MergeIfNeeded(ctrlR, new Message(Ctrl(Keys.T, "T")), RepeatedKeyPolicy.Default);

            var ctrlTAgain = Message.MergeIfNeeded(ctrlT, new Message(Ctrl(Keys.T, "T")), RepeatedKeyPolicy.Default);

            Assert.Equal("Ctrl + T x 2 ", TextOf(ctrlTAgain));
            Assert.Same(ctrlT, ctrlTAgain.Previous);
        }

        [Fact]
        public void a_repeated_shortcut_does_not_take_in_typed_text_and_typed_text_does_not_take_in_a_shortcut()
        {
            var repeated = MergeAll(RepeatedKeyPolicy.Default, new Message(CtrlDown()), new Message(CtrlDown()));
            var typed = new Message(Key("a"));

            Assert.Same(typed, Message.MergeIfNeeded(repeated, typed, RepeatedKeyPolicy.Default));
            Assert.Equal("Ctrl + ↓ x 2 ", TextOf(repeated));

            var shortcut = new Message(CtrlDown());
            Assert.Same(shortcut, Message.MergeIfNeeded(typed, shortcut, RepeatedKeyPolicy.Default));
        }

        [Fact]
        public void a_matched_shortcut_pressed_again_is_still_a_shortcut_and_shows_its_description_once()
        {
            var shortcut = new KeyShortcut("Scroll down");
            var first = new Message(new[] { CtrlDown() }, shortcut, true);
            var second = new Message(new[] { CtrlDown() }, shortcut, true);
            var third = new Message(new[] { CtrlDown() }, shortcut, true);

            var result = MergeAll(RepeatedKeyPolicy.Default, first, second, third);

            Assert.Equal("Ctrl + ↓ x 3 ", TextOf(result));
            Assert.Equal("Scroll down", result.ShortcutName);
            Assert.True(result.IsShortcut);
            Assert.True(result.IsModifier);
            Assert.False(result.CanBeMerged);
        }

        [Fact]
        public void a_shortcut_description_is_kept_in_shortcut_name()
        {
            var result = new Message(new[] { CtrlDown() }, new KeyShortcut("Scroll down"), true);

            Assert.Equal("Ctrl + ↓", TextOf(result));
            Assert.Equal("Scroll down", result.ShortcutName);
        }

        [Fact]
        public void a_shortcut_with_a_different_description_is_a_new_message()
        {
            var first = new Message(new[] { CtrlDown() }, new KeyShortcut("Scroll down"), true);
            var second = new Message(new[] { CtrlDown() }, new KeyShortcut("Next item"), true);
            var unnamed = new Message(CtrlDown());

            Assert.Same(second, Message.MergeIfNeeded(first, second, RepeatedKeyPolicy.Default));
            Assert.Same(unnamed, Message.MergeIfNeeded(first, unnamed, RepeatedKeyPolicy.Default));
        }

        [Fact]
        public void a_shortcut_made_of_several_key_presses_is_not_merged()
        {
            var shortcut = new KeyShortcut("Comment");
            var first = new Message(new[] { Ctrl(Keys.K, "K"), Ctrl(Keys.C, "C") }, shortcut, true);
            var second = new Message(new[] { Ctrl(Keys.K, "K"), Ctrl(Keys.C, "C") }, shortcut, true);

            Assert.Same(second, Message.MergeIfNeeded(first, second, RepeatedKeyPolicy.Default));
            Assert.Equal("Ctrl + K, Ctrl + C", TextOf(second));
            Assert.Equal("Comment", second.ShortcutName);
        }

        [Fact]
        public void the_same_shortcut_in_another_application_is_a_new_message()
        {
            var first = new Message(CtrlDown());
            var other = new Message(new KeyPress(new ProcessInfo("OtherProcess"),
                new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, true, false), false, new[] { "Ctrl", "Down" }));

            Assert.Same(other, Message.MergeIfNeeded(first, other, RepeatedKeyPolicy.Default));
        }

        [Fact]
        public void the_same_shortcut_is_a_new_message_once_the_merge_window_has_passed()
        {
            var first = new Message(CtrlDown());
            Thread.Sleep(TimeSpan.FromMilliseconds(1200));
            var later = new Message(CtrlDown());

            Assert.Same(later, Message.MergeIfNeeded(first, later, RepeatedKeyPolicy.Default));
        }

        [Fact]
        public void a_faded_out_repeated_shortcut_keeps_its_count()
        {
            var repeated = MergeAll(RepeatedKeyPolicy.Default, new Message(CtrlDown()), new Message(CtrlDown()), new Message(CtrlDown()));

            var faded = repeated.FadeOut();

            Assert.Equal("Ctrl + ↓ x 3 ", TextOf(faded));
            Assert.True(faded.IsDeleting);
        }

        static string[] Repeat(string text, int count)
        {
            return Enumerable.Repeat(text, count).ToArray();
        }

        // The policy is applied by the merge, exactly like the message provider does it.
        Message Typed(RepeatedKeyPolicy policy, params string[] texts)
        {
            return MergeAll(policy, texts.Select(text => new Message(Key(text))).ToArray());
        }

        static Message MergeAll(RepeatedKeyPolicy policy, params Message[] messages)
        {
            return messages.Aggregate((merged, next) => Message.MergeIfNeeded(merged, next, policy));
        }

        KeyPress Key(string text)
        {
            return new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.A, KeyDirection.Down, false, false, false), false, new[] { text });
        }

        KeyPress Ctrl(Keys key, string name)
        {
            return new KeyPress(fakeProcess, new InterceptKeyEventArgs(key, KeyDirection.Down, false, true, false), false, new[] { "Ctrl", name });
        }

        KeyPress CtrlDown()
        {
            return Ctrl(Keys.Down, "Down");
        }

        // The key is irrelevant to the display text here, only the text of the input matters.
        Message Typed(params string[] texts)
        {
            return texts
                .Select(text => new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.A, KeyDirection.Down, false, false, false), false, new[] { text })))
                .Aggregate((merged, next) => merged.Merge(next));
        }

        static string TextOf(Message message)
        {
            return string.Join(string.Empty, message.Text);
        }

        Message CreateShortcutMessage(string shortcutName)
        {
            var controlF = new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.F, KeyDirection.Down, false, true, false), false, new[] { "Control", "F" });
            return new Message(new[] { controlF }, new KeyShortcut(shortcutName), true);
        }

        [Fact]
        public void shortcut_name_is_not_part_of_the_text()
        {
            // act
            var result = CreateShortcutMessage("Open the Find Bar");

            // assert
            Assert.Equal("Control + F", string.Join(string.Empty, result.Text));
            Assert.Equal("Open the Find Bar", result.ShortcutName);
            Assert.True(result.HasShortcutName);
        }

        [Fact]
        public void shortcut_name_and_plain_text_survive_fade_out()
        {
            // arrange
            var message = CreateShortcutMessage("Open the Find Bar");

            // act
            var result = message.FadeOut();

            // assert
            Assert.True(result.IsDeleting);
            Assert.Equal("Control + F", string.Join(string.Empty, result.Text));
            Assert.Equal("Open the Find Bar", result.ShortcutName);
        }

        [Fact]
        public void shortcut_without_a_name_has_no_shortcut_name()
        {
            Assert.False(CreateShortcutMessage(null).HasShortcutName);
            Assert.False(CreateShortcutMessage(string.Empty).HasShortcutName);
            Assert.Equal("Control + F", string.Join(string.Empty, CreateShortcutMessage(string.Empty).Text));
        }

        [Fact]
        public void message_that_is_not_a_shortcut_has_no_shortcut_name()
        {
            // act
            var result = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.A, KeyDirection.Down, false, false, false), false, new[] { "a" }));

            // assert
            Assert.Null(result.ShortcutName);
            Assert.False(result.HasShortcutName);
            Assert.Equal("a", string.Join(string.Empty, result.Text));
        }
    }
}