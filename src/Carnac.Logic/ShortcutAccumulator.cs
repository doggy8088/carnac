using System;
using System.Collections.Generic;
using System.Linq;
using Carnac.Logic.Models;

namespace Carnac.Logic
{
    public class ShortcutAccumulator
    {
        List<KeyShortcut> possibleKeyShortcuts;
        Message[] messages;
        readonly List<KeyPress> keys;

        public ShortcutAccumulator()
        {
            keys = new List<KeyPress>();
        }

        public IEnumerable<KeyPress> Keys
        {
            get { return keys; }
        }

        public ShortcutAccumulator ProcessKey(IShortcutProvider shortcutProvider, KeyPress key)
        {
            if (HasCompletedValue)
                return new ShortcutAccumulator().ProcessKey(shortcutProvider, key);

            if (!keys.Any())
            {
                var possibleShortcuts = shortcutProvider.GetShortcutsStartingWith(key);
                if (possibleShortcuts.Any())
                    BeginShortcut(key, possibleShortcuts);
                else
                    Complete(key);

                return this;
            }

            Add(key);
            return this;
        }

        public Message[] GetMessages()
        {
            if (!HasCompletedValue)
                throw new InvalidOperationException();

            return messages;
        }

        public bool HasCompletedValue { get; private set; }

        /// <summary>
        /// True while keys are buffered because they may be the start of a shortcut, and the given key was pressed in
        /// another process. Such a key cannot continue the shortcut: <see cref="Flush"/> the buffered keys and
        /// process the key with a new accumulator.
        /// </summary>
        public bool IsPendingForOtherProcess(KeyPress key)
        {
            return !HasCompletedValue && keys.Count > 0 && keys[0].Process.ProcessName != key.Process.ProcessName;
        }

        /// <summary>
        /// Gives up waiting for the rest of a shortcut: every buffered key becomes an ordinary message of its own.
        /// Does nothing when no keys are buffered or the accumulator has already completed.
        /// </summary>
        public void Flush()
        {
            if (HasCompletedValue || keys.Count == 0)
                return;

            NoMatchingShortcut();
        }

        void Add(KeyPress key)
        {
            var isFirstKey = keys.Count == 0;

            if (!isFirstKey && keys[0].Process.ProcessName != key.Process.ProcessName)
            {
                NoMatchingShortcut();
                return;
            }

            keys.Add(key);
            var newPossibleShortcuts = possibleKeyShortcuts.Where(s => s.StartsWith(keys)).ToList();

            EvaluateShortcuts(newPossibleShortcuts);
        }

        void EvaluateShortcuts(List<KeyShortcut> newPossibleShortcuts)
        {
            if (!newPossibleShortcuts.Any())
                NoMatchingShortcut();
            else if (newPossibleShortcuts.Any(s => s.IsMatch(keys)))
                ShortcutCompleted(newPossibleShortcuts.First(s => s.IsMatch(keys)));
            else
                possibleKeyShortcuts = newPossibleShortcuts;
        }

        void BeginShortcut(KeyPress key, List<KeyShortcut> possibleShortcuts)
        {
            keys.Add(key);
            EvaluateShortcuts(possibleShortcuts);
        }

        void ShortcutCompleted(KeyShortcut shortcut)
        {
            if (HasCompletedValue)
                throw new InvalidOperationException();

            messages = new[] { new Message(Keys, shortcut, true) };
            HasCompletedValue = true;
        }

        void NoMatchingShortcut()
        {
            if (HasCompletedValue)
                throw new InvalidOperationException();

            // When we have no matching shortcut just break all key presses into individual messages
            HasCompletedValue = true;
            messages = keys.Select(k => new Message(k)).ToArray();
        }

        void Complete(KeyPress key)
        {
            if (HasCompletedValue)
                throw new InvalidOperationException();

            HasCompletedValue = true;
            messages = new[] { new Message(key) };
        }
    }
}