using System.Windows.Forms;

namespace Carnac.Logic.MouseMonitor
{
    /// <summary>
    /// Identifies mouse actions as <see cref="Keys"/> values so they can flow through the key press and message pipeline
    /// without colliding with hardware keyboard keys such as <see cref="Keys.VolumeUp"/> and <see cref="Keys.VolumeDown"/>.
    /// </summary>
    public static class MouseKeys
    {
        public const Keys WheelUp = (Keys)0xFF01;
        public const Keys WheelDown = (Keys)0xFF02;

        public static bool IsMouseKey(Keys key)
        {
            switch (key & Keys.KeyCode)
            {
                case Keys.LButton:
                case Keys.RButton:
                case Keys.MButton:
                case Keys.XButton1:
                case Keys.XButton2:
                case WheelUp:
                case WheelDown:
                    return true;
                default:
                    return false;
            }
        }

        public static Keys FromMouseClick(MouseClick click)
        {
            if (click == null)
                return Keys.None;

            switch (click.Button)
            {
                case MouseButtons.Left:
                    return Keys.LButton;
                case MouseButtons.Middle:
                    return Keys.MButton;
                case MouseButtons.Right:
                    return Keys.RButton;
                case MouseButtons.XButton1:
                    return Keys.XButton1;
                case MouseButtons.XButton2:
                    return Keys.XButton2;
                case MouseButtons.None:
                    if (click.WheelDelta > 0)
                        return WheelUp;
                    if (click.WheelDelta < 0)
                        return WheelDown;
                    return Keys.None;
                default:
                    return Keys.None;
            }
        }
    }
}
