using System;
using System.Globalization;

namespace Carnac.Logic.Overlay
{
    /// <summary>
    /// A rectangle in the pixel coordinate space that <c>SetWindowPos</c> uses: the virtual desktop,
    /// origin at the top left of the primary monitor, so monitors left of or above it have negative coordinates.
    /// </summary>
    public struct PixelRect : IEquatable<PixelRect>
    {
        readonly int x;
        readonly int y;
        readonly int width;
        readonly int height;

        public PixelRect(int x, int y, int width, int height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        public int X
        {
            get { return x; }
        }

        public int Y
        {
            get { return y; }
        }

        public int Width
        {
            get { return width; }
        }

        public int Height
        {
            get { return height; }
        }

        /// <summary>The first pixel column to the right of the rectangle.</summary>
        public int Right
        {
            get { return x + width; }
        }

        /// <summary>The first pixel row below the rectangle.</summary>
        public int Bottom
        {
            get { return y + height; }
        }

        public bool Equals(PixelRect other)
        {
            return x == other.x && y == other.y && width == other.width && height == other.height;
        }

        public override bool Equals(object obj)
        {
            return obj is PixelRect && Equals((PixelRect)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = x;
                hash = (hash * 397) ^ y;
                hash = (hash * 397) ^ width;
                hash = (hash * 397) ^ height;
                return hash;
            }
        }

        public static bool operator ==(PixelRect left, PixelRect right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PixelRect left, PixelRect right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0},{1} {2}x{3}", x, y, width, height);
        }
    }
}
