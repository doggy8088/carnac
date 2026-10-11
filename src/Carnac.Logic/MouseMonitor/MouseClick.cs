using System.Windows.Forms;

namespace Carnac.Logic.MouseMonitor
{
    /// <summary>A mouse button press or wheel scroll, and where on the screen (in pixels) it occurred.</summary>
    public sealed class MouseClick
    {
        public MouseClick(int x, int y, MouseButtons button, int wheelDelta = 0, bool altPressed = false, bool controlPressed = false, bool shiftPressed = false)
        {
            X = x;
            Y = y;
            Button = button;
            WheelDelta = wheelDelta;
            AltPressed = altPressed;
            ControlPressed = controlPressed;
            ShiftPressed = shiftPressed;
        }

        public int X { get; private set; }

        public int Y { get; private set; }

        public MouseButtons Button { get; private set; }

        public int WheelDelta { get; private set; }

        public bool AltPressed { get; private set; }

        public bool ControlPressed { get; private set; }

        public bool ShiftPressed { get; private set; }

        /// <summary>
        /// The event comes from the mouse hook, so the state of the keyboard at this moment is the real one.
        /// It is not for events that are made up, as the tests do.
        /// </summary>
        public bool IsFromMouseHook { get; internal set; }
    }
}
