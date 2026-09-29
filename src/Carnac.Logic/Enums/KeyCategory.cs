using System;

namespace Carnac.Logic.Enums
{
    /// <summary>
    /// Groups of keys the user can choose to show or hide. See <see cref="KeyCategories.For"/> for the keys in each group.
    /// </summary>
    [Flags]
    public enum KeyCategory
    {
        None = 0,

        /// <summary>A to Z.</summary>
        Letters = 1,

        /// <summary>0 to 9, on the main keyboard and the numeric keypad.</summary>
        Digits = 2,

        /// <summary>Punctuation and symbol keys, including the numeric keypad operators.</summary>
        Punctuation = 4,

        /// <summary>Space, Enter and Tab.</summary>
        Whitespace = 8,

        /// <summary>Backspace, Delete, Insert and Escape.</summary>
        Editing = 16,

        /// <summary>Arrow keys, Home, End, Page Up and Page Down.</summary>
        Navigation = 32,

        /// <summary>F1 to F24.</summary>
        Function = 64,

        /// <summary>Every other key: Caps Lock, Print Screen, media keys...</summary>
        Other = 128,

        All = Letters | Digits | Punctuation | Whitespace | Editing | Navigation | Function | Other
    }
}
