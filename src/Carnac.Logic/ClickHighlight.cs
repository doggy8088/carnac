using System;
using System.Reflection;
using System.Windows.Media;

namespace Carnac.Logic
{
    /// <summary>A place on the overlay, in device independent units.</summary>
    public sealed class OverlayLocation
    {
        public OverlayLocation(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; private set; }

        public double Y { get; private set; }
    }

    /// <summary>A ring to show around a mouse click.</summary>
    public sealed class ClickHighlight
    {
        public ClickHighlight(OverlayLocation location, string colorName, int diameter, TimeSpan duration)
        {
            Location = location;
            ColorName = colorName;
            Diameter = diameter;
            Duration = duration;
        }

        public OverlayLocation Location { get; private set; }

        /// <summary>The name of a <see cref="Colors"/> member.</summary>
        public string ColorName { get; private set; }

        public int Diameter { get; private set; }

        public TimeSpan Duration { get; private set; }
    }

    /// <summary>What the click highlight settings mean when they are used; saved settings can be anything.</summary>
    public static class ClickHighlightSettings
    {
        public const string DefaultLeftColor = "OrangeRed";
        public const string DefaultMiddleColor = "Gold";
        public const string DefaultRightColor = "RoyalBlue";
        public const int DefaultDiameter = 60;
        public const int DefaultDurationMilliseconds = 600;

        // the sliders on the Mouse tab of the preferences have the same limits
        public const int MinimumDiameter = 20;
        public const int MaximumDiameter = 200;
        public const int MinimumDurationMilliseconds = 100;
        public const int MaximumDurationMilliseconds = 3000;

        public static int GetDiameter(int diameter)
        {
            return Math.Max(MinimumDiameter, Math.Min(MaximumDiameter, diameter));
        }

        public static TimeSpan GetDuration(int milliseconds)
        {
            return TimeSpan.FromMilliseconds(Math.Max(MinimumDurationMilliseconds, Math.Min(MaximumDurationMilliseconds, milliseconds)));
        }

        /// <summary>The name of the color if it is one of the <see cref="Colors"/>, otherwise the fallback.</summary>
        public static string GetColorName(string colorName, string fallback)
        {
            if (!string.IsNullOrEmpty(colorName))
            {
                var color = typeof(Colors).GetProperty(colorName, BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (color != null)
                    return color.Name;
            }

            return fallback;
        }
    }
}
