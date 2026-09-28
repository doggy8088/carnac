using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Carnac.Logic.Internal;
using Carnac.Logic.KeyMonitor;

namespace Carnac.Logic
{
    public class PasswordModeService : IPasswordModeService
    {
        readonly InterceptKeyEventArgsEqualityComparer comparer = new InterceptKeyEventArgsEqualityComparer();
        readonly FixedQueue<InterceptKeyEventArgs> log;
        readonly IKeyDisplayState displayState;
        InterceptKeyEventArgs[] passwordKeyCombination;

        /// <summary>Creates a service with its own private state; the application passes the shared state instead.</summary>
        public PasswordModeService()
            : this(new KeyDisplayState())
        {
        }

        public PasswordModeService(IKeyDisplayState displayState)
        {
            if (displayState == null)
            {
                throw new ArgumentNullException("displayState");
            }

            this.displayState = displayState;
            log = new FixedQueue<InterceptKeyEventArgs>(this.PasswordKeyCombination.Count());
        }

        public bool CheckPasswordMode(InterceptKeyEventArgs key)
        {
            log.Enqueue(key);
            var sortedLog = log.ToList();
            sortedLog.Sort();
            var isMatch = sortedLog.SequenceEqual(PasswordKeyCombination, comparer);
            if (isMatch)
            {
                displayState.ToggleSilent();
                this.log.Clear();
                return true; //this way when the sequence is entered again to EXIT password mode, the key password keycombo doesn't show on screen
            }

            return displayState.IsSilent;
        }

        public IEnumerable<InterceptKeyEventArgs> PasswordKeyCombination
        {
            get
            {
                if (passwordKeyCombination == null)
                {
                    passwordKeyCombination = new[]
                                                 {
                                                     new InterceptKeyEventArgs(Keys.P, KeyDirection.Down,true,true,false), 
                                                 };
                }

                return passwordKeyCombination;
            }
        }

        class InterceptKeyEventArgsEqualityComparer : IEqualityComparer<InterceptKeyEventArgs>
        {
            public bool Equals(InterceptKeyEventArgs x, InterceptKeyEventArgs y)
            {
                if (x == null && y == null)
                {
                    return true;
                }

                if (x == null || y == null)
                {
                    return false;
                }

                return x.Key == y.Key
                       && x.ShiftPressed == y.ShiftPressed
                       && x.AltPressed == y.AltPressed
                       && x.ControlPressed == y.ControlPressed;
            }

            public int GetHashCode(InterceptKeyEventArgs obj)
            {
                return obj.Key.GetHashCode() << obj.AltPressed.GetHashCode()
                       << obj.ShiftPressed.GetHashCode() << obj.ControlPressed.GetHashCode();
            }
        }
    }
}