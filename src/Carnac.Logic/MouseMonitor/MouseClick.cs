using System.Windows.Forms;

namespace Carnac.Logic.MouseMonitor
{
    /// <summary>A mouse button that went down, where on the screen (in pixels).</summary>
    public sealed class MouseClick
    {
        public MouseClick(int x, int y, MouseButtons button)
        {
            X = x;
            Y = y;
            Button = button;
        }

        public int X { get; private set; }

        public int Y { get; private set; }

        public MouseButtons Button { get; private set; }
    }
}
