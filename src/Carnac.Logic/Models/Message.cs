using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;

namespace Carnac.Logic.Models
{
    public sealed class Message
    {
        readonly ReadOnlyCollection<string> textCollection;
        readonly ReadOnlyCollection<KeyPress> keys;
        readonly string processName;
        readonly ImageSource processIcon;
        readonly string shortcutName;
        readonly bool canBeMerged;
        readonly bool isShortcut;
        readonly bool isModifier;
        readonly bool isDeleting;
        readonly DateTime lastMessage;
        readonly Message previous;
        readonly RepeatedKeyPolicy repeatPolicy;

        public Message()
        {
            lastMessage = DateTime.Now;
            repeatPolicy = RepeatedKeyPolicy.Default;
        }

        public Message(KeyPress key)
            : this()
        {
            processName = key.Process.ProcessName;
            processIcon = key.Process.ProcessIcon;
            canBeMerged = !key.HasModifierPressed;
            isModifier = key.HasModifierPressed;

            keys = new ReadOnlyCollection<KeyPress>(new[] { key });
            textCollection = new ReadOnlyCollection<string>(CreateTextSequence(key, repeatPolicy).ToArray());
        }

        public Message(IEnumerable<KeyPress> keys, KeyShortcut shortcut, Boolean isShortcut = false)
            : this(keys, shortcut, isShortcut, RepeatedKeyPolicy.Default)
        {
        }

        private Message(IEnumerable<KeyPress> keys, KeyShortcut shortcut, Boolean isShortcut, RepeatedKeyPolicy repeatPolicy)
            : this()
        {
            this.repeatPolicy = repeatPolicy;
            var allKeys = keys.ToArray();
            var distinctProcessName = allKeys.Select(k => k.Process.ProcessName)
                .Distinct()
                .ToArray();
            if (distinctProcessName.Count() != 1)
                throw new InvalidOperationException("Keys are from different processes");

            processName = distinctProcessName.Single();
            processIcon = allKeys.First().Process.ProcessIcon;
            shortcutName = shortcut.Name;
            this.isShortcut = isShortcut;
            this.isModifier = allKeys.Any(k => k.HasModifierPressed);
            canBeMerged = false;

            this.keys = new ReadOnlyCollection<KeyPress>(allKeys);

            var textSeq = CreateTextSequence(allKeys, repeatPolicy).ToList();
            if (!string.IsNullOrEmpty(shortcutName))
            {
                // a summarised run ends with a space already ("Ctrl + Down x 3 ")
                var endsWithSpace = textSeq.Count > 0 && textSeq[textSeq.Count - 1].EndsWith(" ", StringComparison.Ordinal);
                textSeq.Add(string.Format(endsWithSpace ? "[{0}]" : " [{0}]", shortcutName));
            }
            textCollection = new ReadOnlyCollection<string>(textSeq);
        }

        private Message(Message initial, Message appended, RepeatedKeyPolicy repeatPolicy)
            : this(initial.keys.Concat(appended.keys), new KeyShortcut(initial.ShortcutName), initial.isShortcut && appended.isShortcut, repeatPolicy)
        {
            previous = initial;

            // Two shortcut messages only merge when they are the same shortcut pressed again. That message
            // stays closed to typed text, exactly like the single shortcut it started from. Any other merge
            // (typed text) is open for more, as it always was.
            canBeMerged = initial.canBeMerged || appended.canBeMerged;
        }

        private Message(Message initial, bool isDeleting)
            : this(initial.keys, new KeyShortcut(initial.ShortcutName), false, initial.repeatPolicy)
        {
            this.isDeleting = isDeleting;
            previous = initial;
            lastMessage = initial.lastMessage;
        }

        public string ProcessName { get { return processName; } }

        public ImageSource ProcessIcon { get { return processIcon; } }

        public string ShortcutName { get { return shortcutName; } }

        public bool CanBeMerged { get { return canBeMerged; } }

        public bool IsShortcut { get { return isShortcut; } }

        public Message Previous { get { return previous; } }

        public ReadOnlyCollection<string> Text { get { return textCollection; } }

        public DateTime LastMessage { get { return lastMessage; } }

        public bool IsDeleting { get { return isDeleting; } }

        public bool IsModifier { get { return isModifier; } }

        public Message Merge(Message other)
        {
            return new Message(this, other, repeatPolicy);
        }

        static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Appends <paramref name="newMessage"/> to <paramref name="previousMessage"/> when they belong together
        /// (typed text, or the same shortcut pressed again), otherwise returns <paramref name="newMessage"/>.
        /// The merged message is written with <paramref name="repeatPolicy"/>, and keeps it for later.
        /// </summary>
        public static Message MergeIfNeeded(Message previousMessage, Message newMessage, RepeatedKeyPolicy repeatPolicy)
        {
            if (previousMessage == null) throw new ArgumentNullException("previousMessage");
            if (newMessage == null) throw new ArgumentNullException("newMessage");
            if (repeatPolicy == null) throw new ArgumentNullException("repeatPolicy");

            return ShouldCreateNewMessage(previousMessage, newMessage)
                ? newMessage
                : new Message(previousMessage, newMessage, repeatPolicy);
        }

        static bool ShouldCreateNewMessage(Message previous, Message current)
        {
            if (previous.ProcessName != current.ProcessName ||
                current.LastMessage.Subtract(previous.LastMessage) > OneSecond)
                return true;

            if (previous.CanBeMerged && current.CanBeMerged)
                return false;

            return !IsSameShortcutPressedAgain(previous, current);
        }

        // "Ctrl + Down" pressed three times is one message ("Ctrl + Down x 3") instead of three. Only a single
        // press that repeats the message's own key press counts: different shortcuts, and shortcuts made of
        // several key presses (chords), each keep their own message.
        static bool IsSameShortcutPressedAgain(Message previous, Message current)
        {
            return previous.keys != null
                && current.keys != null
                && current.keys.Count == 1
                && !previous.CanBeMerged
                && !current.CanBeMerged
                && previous.isShortcut == current.isShortcut
                && string.Equals(previous.shortcutName, current.shortcutName, StringComparison.Ordinal)
                && previous.keys.All(key => RepeatedKeyPress.IsSamePress(key, current.keys[0]));
        }

        public Message FadeOut()
        {
            return new Message(this, true);
        }

        static IEnumerable<string> CreateTextSequence(KeyPress key, RepeatedKeyPolicy repeatPolicy)
        {
            return CreateTextSequence(new[] {key}, repeatPolicy);
        }

        static IEnumerable<string> CreateTextSequence(IEnumerable<KeyPress> keys, RepeatedKeyPolicy repeatPolicy)
        {
            return keys.Aggregate(new List<RepeatedKeyPress>(),
              (acc, curr) =>
              {
                  if (acc.Any())
                  {
                      var last = acc.Last();
                      if (last.IsRepeatedBy(curr))
                      {
                          last.IncrementRepeat();
                      }
                      else
                      {
                          acc.Add(new RepeatedKeyPress(curr, repeatPolicy, last.NextRequiresSeperator));
                      }
                  }
                  else
                  {
                      acc.Add(new RepeatedKeyPress(curr, repeatPolicy));
                  }
                  return acc;
              })
              .SelectMany(rkp => rkp.GetTextParts());
        }

        public override string ToString()
        {
            return string.Format("{0} {1} {2}", ProcessName, string.Join(string.Empty, Text), ShortcutName);
        }

        private sealed class RepeatedKeyPress
        {
            readonly KeyPress keyPress;
            readonly RepeatedKeyPolicy repeatPolicy;
            readonly bool requiresPrefix;
            readonly bool nextRequiresSeperator;
            readonly string[] textParts;
            int repeatCount;
            int? minimumRepeatToSummarise;

            public RepeatedKeyPress(KeyPress keyPress, RepeatedKeyPolicy repeatPolicy, bool requiresPrefix = false)
            {
                this.keyPress = keyPress;
                this.repeatPolicy = repeatPolicy;
                nextRequiresSeperator = keyPress.HasModifierPressed;
                textParts = keyPress.GetTextParts().ToArray();
                this.requiresPrefix = requiresPrefix;
                repeatCount = 1;
            }

            // How many presses in a row it takes before "x N" is shown. The policy decides for typed
            // characters; named keys ("Back", "Left") and anything pressed with a modifier collapse from two.
            int MinimumRepeatToSummarise()
            {
                if (!minimumRepeatToSummarise.HasValue)
                {
                    var typedCharacter = GetTypedCharacter(keyPress);
                    minimumRepeatToSummarise = typedCharacter == null
                        ? repeatPolicy.MinimumNamedKeyRepeat
                        : repeatPolicy.GetMinimumTypedCharacterRepeat(char.IsDigit(typedCharacter, 0));
                }

                return minimumRepeatToSummarise.Value;
            }

            // The single visible character (or a space) this key press types, otherwise null. The raw input is
            // used rather than the formatted text ("Left" is drawn as an arrow glyph, which is not typing), and
            // not the modifier flags: a shortcut has its modifiers in the input ("Ctrl", "L"), while a character
            // that is typed with AltGr (which Windows reports as Ctrl+Alt) is given as a single character once
            // the keys are named after the keyboard layout. With the US names it is still "Ctrl", "Alt", "7".
            static string GetTypedCharacter(KeyPress keyPress)
            {
                var input = keyPress.Input.ToArray();
                if (input.Length != 1 || string.IsNullOrEmpty(input[0]))
                    return null;

                // the numpad operators are padded with spaces (" + ") to read well in a sentence
                var text = input[0] == " " ? input[0] : input[0].Trim();
                var isSingleCharacter = text.Length == 1 || (text.Length == 2 && char.IsSurrogatePair(text, 0));
                if (!isSingleCharacter)
                    return null;

                var isTypedCharacter = text == " "
                    || char.IsLetter(text, 0)
                    || char.IsNumber(text, 0)
                    || char.IsPunctuation(text, 0)
                    || char.IsSymbol(text, 0);
                return isTypedCharacter ? text : null;
            }

            public bool NextRequiresSeperator { get { return nextRequiresSeperator; } }

            public void IncrementRepeat()
            {
                repeatCount++;
            }

            public bool IsRepeatedBy(KeyPress nextKeyPress)
            {
                return IsSamePress(keyPress, nextKeyPress);
            }

            public static bool IsSamePress(KeyPress first, KeyPress second)
            {
                // the modifier state decides how a run is summarised, so a run must not mix the two
                return first.HasModifierPressed == second.HasModifierPressed
                    && first.GetTextParts().SequenceEqual(second.GetTextParts());
            }

            public IEnumerable<string> GetTextParts()
            {
                if (requiresPrefix)
                    yield return ", ";

                var summarise = repeatCount > 1 && repeatCount >= MinimumRepeatToSummarise();
                var copies = summarise ? 1 : repeatCount;
                for (var copy = 0; copy < copies; copy++)
                {
                    foreach (var textPart in textParts)
                    {
                        yield return textPart;
                    }
                }
                if (summarise)
                    yield return string.Format(" x {0} ", repeatCount);
            }
        }

        #region Equality overrides

        bool Equals(Message other)
        {
            return textCollection.SequenceEqual(other.textCollection)
                && keys.SequenceEqual(other.keys)
                && string.Equals(processName, other.processName)
                && Equals(processIcon, other.processIcon)
                && string.Equals(shortcutName, other.shortcutName)
                && canBeMerged.Equals(other.canBeMerged)
                && isShortcut.Equals(other.isShortcut)
                && isDeleting.Equals(other.isDeleting)
                && lastMessage.Equals(other.lastMessage);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Message)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (textCollection != null ? textCollection.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (keys != null ? keys.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (processName != null ? processName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (processIcon != null ? processIcon.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (shortcutName != null ? shortcutName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ canBeMerged.GetHashCode();
                hashCode = (hashCode * 397) ^ isShortcut.GetHashCode();
                hashCode = (hashCode * 397) ^ isDeleting.GetHashCode();
                hashCode = (hashCode * 397) ^ lastMessage.GetHashCode();
                return hashCode;
            }
        }
        #endregion
    }
}
