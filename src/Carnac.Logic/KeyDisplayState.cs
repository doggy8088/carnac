using System;

namespace Carnac.Logic
{
    /// <summary>Thread-safe implementation of <see cref="IKeyDisplayState"/>.</summary>
    public class KeyDisplayState : IKeyDisplayState
    {
        readonly object sync = new object();
        bool isPaused;
        bool isSilent;

        public event EventHandler Changed;

        public bool IsPaused
        {
            get
            {
                lock (sync)
                {
                    return isPaused;
                }
            }
        }

        public bool IsSilent
        {
            get
            {
                lock (sync)
                {
                    return isSilent;
                }
            }
        }

        public void SetPaused(bool paused)
        {
            bool changed;
            lock (sync)
            {
                changed = isPaused != paused;
                isPaused = paused;
            }

            if (changed)
                OnChanged();
        }

        public void SetSilent(bool silent)
        {
            bool changed;
            lock (sync)
            {
                changed = isSilent != silent;
                isSilent = silent;
            }

            if (changed)
                OnChanged();
        }

        public bool TogglePaused()
        {
            bool result;
            lock (sync)
            {
                isPaused = !isPaused;
                result = isPaused;
            }

            OnChanged();
            return result;
        }

        public bool ToggleSilent()
        {
            bool result;
            lock (sync)
            {
                isSilent = !isSilent;
                result = isSilent;
            }

            OnChanged();
            return result;
        }

        void OnChanged()
        {
            // Raised outside the lock so handlers can read the state without risking a deadlock.
            var handler = Changed;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
