using System;
using System.Collections.Generic;
using System.Linq;

namespace Carnac.Logic.Native
{
    /// <summary>
    /// Draws the screens the way they are arranged on the desk: every screen keeps its position relative to the
    /// others, so a vertical stack or an L-shaped arrangement looks like it does in the Windows display settings.
    /// </summary>
    public class MonitorLayout
    {
        /// <summary>The widest screen is drawn at most this wide (the size a screen had in the old single row).</summary>
        public const double MaxTileWidth = 200;

        /// <summary>The whole arrangement is drawn at most this wide.</summary>
        public const double MaxWidth = 560;

        /// <summary>The whole arrangement is drawn at most this high.</summary>
        public const double MaxHeight = 140;

        MonitorLayout(double width, double height)
        {
            Width = width;
            Height = height;
        }

        /// <summary>The width of the drawn arrangement.</summary>
        public double Width { get; private set; }

        /// <summary>The height of the drawn arrangement.</summary>
        public double Height { get; private set; }

        /// <summary>
        /// Sets <see cref="DetailedScreen.RelativeWidth"/>, <see cref="DetailedScreen.RelativeHeight"/>,
        /// <see cref="DetailedScreen.LayoutLeft"/> and <see cref="DetailedScreen.LayoutTop"/> of every screen from
        /// its physical position and size. Screens without a size are drawn as nothing.
        /// </summary>
        public static MonitorLayout Create(IEnumerable<DetailedScreen> screens)
        {
            if (screens == null) throw new ArgumentNullException("screens");

            var all = screens.Where(s => s != null).ToList();
            foreach (var screen in all)
            {
                screen.RelativeWidth = 0;
                screen.RelativeHeight = 0;
                screen.LayoutLeft = 0;
                screen.LayoutTop = 0;
            }

            var drawable = all.Where(s => s.Width > 0 && s.Height > 0).ToList();
            if (drawable.Count == 0)
                return new MonitorLayout(0, 0);

            var left = drawable.Min(s => s.Left);
            var top = drawable.Min(s => s.Top);
            var totalWidth = drawable.Max(s => s.Left + s.Width) - left;
            var totalHeight = drawable.Max(s => s.Top + s.Height) - top;
            var widest = drawable.Max(s => s.Width);

            var scale = Math.Min(MaxTileWidth / widest, Math.Min(MaxWidth / totalWidth, MaxHeight / totalHeight));

            foreach (var screen in drawable)
            {
                screen.RelativeWidth = screen.Width * scale;
                screen.RelativeHeight = screen.Height * scale;
                screen.LayoutLeft = (screen.Left - left) * scale;
                screen.LayoutTop = (screen.Top - top) * scale;
            }

            return new MonitorLayout(totalWidth * scale, totalHeight * scale);
        }
    }
}
