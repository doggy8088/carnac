using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    public class KeyCombinationParserFacts
    {
        static KeyPressDefinition Parse(string text)
        {
            KeyPressDefinition definition;
            string error;
            Assert.True(KeyCombinationParser.TryParse(text, out definition, out error), "'" + text + "' should parse but: " + error);
            Assert.Null(error);
            return definition;
        }

        static string ParseError(string text)
        {
            KeyPressDefinition definition;
            string error;
            Assert.False(KeyCombinationParser.TryParse(text, out definition, out error), "'" + text + "' should not parse");
            Assert.Null(definition);
            Assert.False(string.IsNullOrEmpty(error));
            return error;
        }

        static IList<KeyPressDefinition> ParseSequence(string text)
        {
            IList<KeyPressDefinition> sequence;
            string error;
            Assert.True(KeyCombinationParser.TryParseSequence(text, out sequence, out error), "'" + text + "' should parse but: " + error);
            return sequence;
        }

        static string SequenceError(string text)
        {
            IList<KeyPressDefinition> sequence;
            string error;
            Assert.False(KeyCombinationParser.TryParseSequence(text, out sequence, out error), "'" + text + "' should not parse");
            Assert.Null(sequence);
            Assert.False(string.IsNullOrEmpty(error));
            return error;
        }

        [Fact]
        public void plain_key_has_no_modifiers()
        {
            Assert.Equal(new KeyPressDefinition(Keys.Enter), Parse("Enter"));
        }

        [Fact]
        public void all_modifiers_are_recognised()
        {
            var expected = new KeyPressDefinition(Keys.N, winkeyPressed: true, shiftPressed: true, altPressed: true, controlPressed: true);

            Assert.Equal(expected, Parse("Ctrl+Alt+Shift+Win+N"));
        }

        [Fact]
        public void modifiers_and_keys_are_case_insensitive_and_order_independent()
        {
            var expected = new KeyPressDefinition(Keys.PageDown, shiftPressed: true, altPressed: true);

            Assert.Equal(expected, Parse("shift+ALT+pagedown"));
            Assert.Equal(expected, Parse("Alt+Shift+PageDown"));
        }

        [Fact]
        public void control_and_windows_are_accepted_as_modifier_names()
        {
            Assert.Equal(new KeyPressDefinition(Keys.E, winkeyPressed: true, controlPressed: true), Parse("Control+Windows+E"));
        }

        [Fact]
        public void whitespace_around_the_parts_is_ignored()
        {
            Assert.Equal(new KeyPressDefinition(Keys.S, controlPressed: true), Parse("  Ctrl + S "));
        }

        [Fact]
        public void a_word_that_only_contains_a_modifier_name_is_not_a_modifier()
        {
            // The old parser used string.Contains, so "Shifty" and "Alternate" counted as Shift and Alt.
            Assert.Contains("'Shifty'", ParseError("Shifty+A"));
            Assert.Contains("'Alternate'", ParseError("Alternate+A"));
        }

        [Fact]
        public void the_modifiers_are_not_taken_from_the_key_name()
        {
            // "Ctrl" inside the key part must not switch the control modifier on.
            Assert.False(Parse("Enter").ControlPressed);
            Assert.False(Parse("Alt+Left").ShiftPressed);
        }

        [Fact]
        public void a_key_before_the_last_part_is_rejected_instead_of_being_ignored()
        {
            // "F10+Enter" used to be read as a plain Enter, which labelled every Enter in Chrome.
            var error = ParseError("F10+Enter");

            Assert.Contains("'F10'", error);
        }

        [Fact]
        public void unknown_key_names_are_rejected_with_the_offending_text()
        {
            Assert.Contains("'Up Arrow'", ParseError("Up Arrow"));
            Assert.Contains("'Left arrow'", ParseError("Ctrl+Left arrow"));
            Assert.Contains("'Esc'", ParseError("Esc"));
        }

        [Fact]
        public void unknown_key_names_suggest_the_name_the_parser_understands()
        {
            Assert.Contains("did you mean 'Up'", ParseError("Up Arrow"));
            Assert.Contains("did you mean 'Escape'", ParseError("Esc"));
            Assert.Contains("did you mean 'Back'", ParseError("Ctrl+Backspace"));
            Assert.Contains("did you mean 'PageDown'", ParseError("PgDn"));
        }

        [Fact]
        public void wording_instead_of_plus_is_rejected()
        {
            ParseError("Ctrl and +");
            ParseError("Ctrl and -");
        }

        [Fact]
        public void space_separated_chords_are_rejected()
        {
            // Chords are written with commas: "Ctrl+K,Ctrl+D".
            ParseError("Ctrl+K Ctrl+D");
            ParseError("Escape Escape");
        }

        [Fact]
        public void empty_and_incomplete_entries_are_rejected()
        {
            ParseError(null);
            ParseError("");
            ParseError("   ");
            ParseError("Ctrl+");
            ParseError("+Ctrl+A");
            ParseError("Ctrl++A");
            ParseError("Ctrl+Ctrl+A");
        }

        [Fact]
        public void a_modifier_without_a_key_is_rejected()
        {
            Assert.Contains("modifier", ParseError("Shift"));
            Assert.Contains("modifier", ParseError("Ctrl+Shift"));
            Assert.Contains("modifier", ParseError("Win"));
        }

        [Fact]
        public void physical_modifier_keys_are_rejected_because_they_can_never_match()
        {
            foreach (var name in new[] { "LControlKey", "RControlKey", "ControlKey", "LShiftKey", "RShiftKey", "ShiftKey", "LMenu", "RMenu", "Menu", "LWin", "RWin" })
            {
                Assert.Contains("modifier", ParseError(name));
                Assert.Contains("modifier", ParseError("Ctrl+" + name));
            }
        }

        [Fact]
        public void characters_typed_with_shift_mean_the_shifted_key()
        {
            // Ctrl+! is pressed as Ctrl+Shift+1, Ctrl+? as Ctrl+Shift+/, and so on: the real key event carries the Shift flag.
            Assert.Equal(new KeyPressDefinition(Keys.D1, controlPressed: true, shiftPressed: true), Parse("Ctrl+!"));
            Assert.Equal(new KeyPressDefinition(Keys.OemQuestion, controlPressed: true, shiftPressed: true), Parse("Ctrl+?"));
            Assert.Equal(new KeyPressDefinition(Keys.OemMinus, altPressed: true, shiftPressed: true), Parse("Alt+_"));
            // the unshifted keys and explicit names stay unshifted
            Assert.Equal(new KeyPressDefinition(Keys.Oemplus, controlPressed: true), Parse("Ctrl+="));
            Assert.Equal(new KeyPressDefinition(Keys.OemQuestion, controlPressed: true), Parse("Ctrl+/"));
            Assert.Equal(new KeyPressDefinition(Keys.Oemplus, controlPressed: true), Parse("Ctrl+Oemplus"));
        }

        [Fact]
        public void the_ins_and_del_aliases_do_not_imply_shift()
        {
            Assert.Equal(new KeyPressDefinition(Keys.Insert, controlPressed: true), Parse("Ctrl+ins"));
            Assert.Equal(new KeyPressDefinition(Keys.Delete, controlPressed: true), Parse("Ctrl+del"));
        }

        [Fact]
        public void a_single_combination_cannot_contain_a_comma_separated_list_of_key_names()
        {
            // Enum.TryParse would OR "A,B" into the key C; the single-combination API has to reject it.
            Assert.Contains("comma", ParseError("A,B"));
            Assert.Contains("comma", ParseError("Ctrl+A,B"));
            Assert.Contains("comma", ParseError("Ctrl+A, Shift"));
            Assert.Contains("comma", ParseError("1,2"));
            // the comma key itself, and a chord, still work
            Assert.Equal(new KeyPressDefinition(Keys.Oemcomma, controlPressed: true), Parse("Ctrl+,"));
            Assert.Equal(new KeyPressDefinition(Keys.Oemcomma), Parse("Oemcomma"));
            Assert.Equal(2, ParseSequence("A,B").Count);
        }

        [Fact]
        public void numbers_other_than_single_digits_are_not_key_codes()
        {
            // Enum parsing reads "112" as the key code of F1 and "1" as the left mouse button.
            ParseError("112");
            Assert.Equal(new KeyPressDefinition(Keys.D1, controlPressed: true), Parse("Ctrl+1"));
            Assert.Equal(new KeyPressDefinition(Keys.D0), Parse("0"));
            Assert.Equal(new KeyPressDefinition(Keys.D9, controlPressed: true), Parse("Ctrl+9"));
        }

        [Fact]
        public void punctuation_keys_can_be_written_as_characters()
        {
            Assert.Equal(new KeyPressDefinition(Keys.OemMinus, controlPressed: true), Parse("Ctrl+-"));
            Assert.Equal(new KeyPressDefinition(Keys.OemPeriod, controlPressed: true, shiftPressed: true), Parse("Ctrl+Shift+."));
            Assert.Equal(new KeyPressDefinition(Keys.OemQuestion, controlPressed: true), Parse("Ctrl+/"));
            Assert.Equal(new KeyPressDefinition(Keys.Oem5, controlPressed: true), Parse("Ctrl+\\"));
            Assert.Equal(new KeyPressDefinition(Keys.Oemcomma, controlPressed: true), Parse("Ctrl+,"));
        }

        [Fact]
        public void the_plus_key_is_written_as_a_trailing_double_plus()
        {
            // the plus character is typed with Shift, so it means the shifted key
            Assert.Equal(new KeyPressDefinition(Keys.Oemplus, controlPressed: true, shiftPressed: true), Parse("Ctrl++"));
            Assert.Equal(new KeyPressDefinition(Keys.Oemplus, controlPressed: true, shiftPressed: true), Parse("Ctrl+Shift++"));
            Assert.Equal(new KeyPressDefinition(Keys.Oemplus, shiftPressed: true), Parse("+"));
            Assert.Equal(new KeyPressDefinition(Keys.Oemplus, controlPressed: true), Parse("Ctrl+Oemplus"));
            Assert.Equal(new KeyPressDefinition(Keys.Add, controlPressed: true), Parse("Ctrl+Add"));
        }

        [Fact]
        public void sequence_is_split_on_commas()
        {
            var sequence = ParseSequence("Ctrl+K,Ctrl+C");

            Assert.Equal(
                new[] { new KeyPressDefinition(Keys.K, controlPressed: true), new KeyPressDefinition(Keys.C, controlPressed: true) },
                sequence.ToArray());
        }

        [Fact]
        public void whitespace_around_commas_is_ignored()
        {
            var sequence = ParseSequence("Ctrl+M, B");

            Assert.Equal(
                new[] { new KeyPressDefinition(Keys.M, controlPressed: true), new KeyPressDefinition(Keys.B) },
                sequence.ToArray());
        }

        [Fact]
        public void single_combination_is_a_sequence_of_one()
        {
            Assert.Equal(1, ParseSequence("Ctrl+Shift+N").Count);
        }

        [Fact]
        public void konami_style_sequence_keeps_every_key()
        {
            Assert.Equal(10, ParseSequence("Up,Up,Down,Down,Left,Right,Left,Right,B,A").Count);
        }

        [Fact]
        public void a_comma_after_a_plus_is_the_comma_key()
        {
            var sequence = ParseSequence("Ctrl+,");
            Assert.Equal(new[] { new KeyPressDefinition(Keys.Oemcomma, controlPressed: true) }, sequence.ToArray());

            sequence = ParseSequence("Ctrl+K,Ctrl+,");
            Assert.Equal(
                new[] { new KeyPressDefinition(Keys.K, controlPressed: true), new KeyPressDefinition(Keys.Oemcomma, controlPressed: true) },
                sequence.ToArray());
        }

        [Fact]
        public void a_comma_after_the_plus_key_separates_the_next_combination()
        {
            // "Ctrl++" is already complete (Ctrl and the plus key), so the comma after it separates two combinations.
            Assert.Equal(
                new[] { new KeyPressDefinition(Keys.Oemplus, controlPressed: true, shiftPressed: true), new KeyPressDefinition(Keys.A) },
                ParseSequence("Ctrl++,A").ToArray());
            Assert.Equal(
                new[] { new KeyPressDefinition(Keys.Oemplus, shiftPressed: true), new KeyPressDefinition(Keys.A) },
                ParseSequence("+,A").ToArray());
            Assert.Equal(
                new[] { new KeyPressDefinition(Keys.Oemplus, controlPressed: true, shiftPressed: true), new KeyPressDefinition(Keys.Oemcomma, controlPressed: true) },
                ParseSequence("Ctrl++, Ctrl+,").ToArray());
            // comma key, then the separating comma, then Ctrl and the plus key
            Assert.Equal(
                new[]
                {
                    new KeyPressDefinition(Keys.Oemcomma, controlPressed: true, shiftPressed: true),
                    new KeyPressDefinition(Keys.Oemplus, controlPressed: true, shiftPressed: true)
                },
                ParseSequence("Ctrl+Shift+,,Ctrl++").ToArray());
        }

        [Fact]
        public void a_lone_comma_key_is_written_as_a_name()
        {
            Assert.Equal(new KeyPressDefinition(Keys.Oemcomma), Parse("Oemcomma"));
            SequenceError(",");
            SequenceError("A,,B");
        }

        [Fact]
        public void one_bad_part_rejects_the_whole_sequence()
        {
            // The old parser silently dropped the bad part and kept a truncated chord.
            var error = SequenceError("Ctrl+K,Ctrl+Bogus");

            Assert.Contains("Ctrl+Bogus", error);
            Assert.Contains("'Bogus'", error);
        }

        [Fact]
        public void empty_parts_reject_the_sequence()
        {
            SequenceError("");
            SequenceError(null);
            SequenceError("Ctrl+K,");
        }

        [Fact]
        public void instance_reports_a_warning_with_context_and_returns_null()
        {
            var warnings = new List<string>();
            var parser = new KeyCombinationParser(warnings.Add);

            var sequence = parser.ParseSequence("Ctrl+K Ctrl+D", "vscode.yml, shortcut 'Delete line'");

            Assert.Null(sequence);
            var warning = Assert.Single(warnings);
            Assert.Contains("vscode.yml, shortcut 'Delete line'", warning);
            Assert.Contains("Ctrl+K Ctrl+D", warning);
        }

        [Fact]
        public void instance_stays_quiet_for_valid_text()
        {
            var warnings = new List<string>();
            var parser = new KeyCombinationParser(warnings.Add);

            Assert.Equal(2, parser.ParseSequence("Ctrl+K,Ctrl+C", "context").Count);
            Assert.Equal(new KeyPressDefinition(Keys.A, controlPressed: true), parser.ParseCombination("Ctrl+A", "context"));

            Assert.Empty(warnings);
        }

        [Fact]
        public void instance_reports_single_combination_failures()
        {
            var warnings = new List<string>();
            var parser = new KeyCombinationParser(warnings.Add);

            Assert.Null(parser.ParseCombination("Bogus", "ignore list"));

            var warning = Assert.Single(warnings);
            Assert.Contains("ignore list", warning);
            Assert.Contains("Bogus", warning);
        }
    }
}
