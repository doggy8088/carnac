using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.Enums;
using Xunit;

namespace Carnac.Tests
{
    public class KeyCategoriesFacts
    {
        static void AssertCategory(KeyCategory expected, params Keys[] keys)
        {
            foreach (var key in keys)
                Assert.True(expected == KeyCategories.For(key), key + " should be " + expected + " but is " + KeyCategories.For(key));
        }

        [Fact]
        public void letters()
        {
            AssertCategory(KeyCategory.Letters, Keys.A, Keys.L, Keys.Z);
        }

        [Fact]
        public void digits_on_the_main_keyboard_and_the_numeric_keypad()
        {
            AssertCategory(KeyCategory.Digits, Keys.D0, Keys.D1, Keys.D9, Keys.NumPad0, Keys.NumPad5, Keys.NumPad9);
        }

        [Fact]
        public void punctuation_and_symbols()
        {
            AssertCategory(KeyCategory.Punctuation,
                Keys.Oem1, Keys.Oemplus, Keys.Oemcomma, Keys.OemMinus, Keys.OemPeriod, Keys.OemQuestion, Keys.Oemtilde,
                Keys.OemOpenBrackets, Keys.Oem5, Keys.Oem6, Keys.Oem7, Keys.Oem8, Keys.OemBackslash,
                Keys.Multiply, Keys.Add, Keys.Separator, Keys.Subtract, Keys.Decimal, Keys.Divide);
        }

        [Fact]
        public void space_enter_and_tab_are_whitespace()
        {
            AssertCategory(KeyCategory.Whitespace, Keys.Space, Keys.Enter, Keys.Tab);
        }

        [Fact]
        public void backspace_delete_insert_and_escape_are_editing_keys()
        {
            AssertCategory(KeyCategory.Editing, Keys.Back, Keys.Delete, Keys.Insert, Keys.Escape);
        }

        [Fact]
        public void arrows_home_end_and_paging_are_navigation()
        {
            AssertCategory(KeyCategory.Navigation, Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown);
        }

        [Fact]
        public void f1_to_f24_are_function_keys()
        {
            for (var key = Keys.F1; key <= Keys.F24; key++)
                AssertCategory(KeyCategory.Function, key);
        }

        [Fact]
        public void everything_else_is_other()
        {
            AssertCategory(KeyCategory.Other,
                Keys.CapsLock, Keys.NumLock, Keys.Scroll, Keys.PrintScreen, Keys.Pause, Keys.Apps, Keys.LWin, Keys.Sleep,
                Keys.VolumeUp, Keys.MediaPlayPause, Keys.BrowserBack, Keys.None);
        }

        [Fact]
        public void modifier_flags_in_the_key_value_do_not_change_the_category()
        {
            Assert.Equal(KeyCategory.Letters, KeyCategories.For(Keys.A | Keys.Control | Keys.Shift));
            Assert.Equal(KeyCategory.Function, KeyCategories.For(Keys.F5 | Keys.Alt));
        }

        [Fact]
        public void every_key_has_exactly_one_known_category()
        {
            var known = new List<KeyCategory>
            {
                KeyCategory.Letters, KeyCategory.Digits, KeyCategory.Punctuation, KeyCategory.Whitespace,
                KeyCategory.Editing, KeyCategory.Navigation, KeyCategory.Function, KeyCategory.Other
            };

            foreach (Keys key in Enum.GetValues(typeof(Keys)))
            {
                if ((key & Keys.Modifiers) != Keys.None)
                    continue;

                var category = KeyCategories.For(key);
                Assert.True(known.Contains(category), key + " has category " + category);
            }
        }

        [Fact]
        public void all_is_the_combination_of_every_category()
        {
            Assert.Equal(KeyCategory.All, Enum.GetValues(typeof(KeyCategory)).Cast<KeyCategory>().Where(c => c != KeyCategory.All)
                .Aggregate(KeyCategory.None, (all, category) => all | category));
        }
    }
}
