using System;
using System.Windows;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    public class WindowBoundsClamperFacts
    {
        static readonly Size MinimumSize = new Size(610, 430);

        // Single 1920x1080 monitor with a 40 pixel taskbar at the bottom.
        static readonly Rect Primary = new Rect(0, 0, 1920, 1040);

        static Rect Clamp(Rect desired, params Rect[] workAreas)
        {
            return WindowBoundsClamper.Clamp(desired, MinimumSize, workAreas, Primary);
        }

        static Rect CentredOnPrimary
        {
            get { return new Rect((1920 - 610) / 2.0, (1040 - 430) / 2.0, 610, 430); }
        }

        [Fact]
        public void WindowFullyOnAScreenIsNotChanged()
        {
            var desired = new Rect(100, 120, 610, 430);

            Assert.Equal(desired, Clamp(desired, Primary));
        }

        [Fact]
        public void WindowOnTheScreenEdgeIsNotChanged()
        {
            var desired = new Rect(1310, 610, 610, 430);

            Assert.Equal(desired, Clamp(desired, Primary));
        }

        [Fact]
        public void WindowCompletelyOffScreenIsCentredOnThePrimaryMonitor()
        {
            var result = Clamp(new Rect(5000, 4000, 610, 430), Primary);

            Assert.Equal(CentredOnPrimary, result);
        }

        [Fact]
        public void WindowLeftOnAnUnpluggedMonitorIsCentredOnThePrimaryMonitor()
        {
            // The second monitor used to sit to the right of the primary one.
            var result = Clamp(new Rect(2100, 200, 610, 430), Primary);

            Assert.Equal(CentredOnPrimary, result);
        }

        [Fact]
        public void WindowWithOnlyAFewPixelsOnScreenIsCentredOnThePrimaryMonitor()
        {
            var result = Clamp(new Rect(1910, 200, 610, 430), Primary);

            Assert.Equal(CentredOnPrimary, result);
        }

        [Fact]
        public void WindowWithoutAPositionIsCentredOnThePrimaryMonitor()
        {
            var result = Clamp(new Rect(double.NaN, double.NaN, 610, 430), Primary);

            Assert.Equal(CentredOnPrimary, result);
        }

        [Fact]
        public void EmptyBoundsAreCentredOnThePrimaryMonitorWithMinimumSize()
        {
            var result = Clamp(Rect.Empty, Primary);

            Assert.Equal(CentredOnPrimary, result);
        }

        [Fact]
        public void WindowPartiallyOffTheLeftEdgeIsMovedBackInside()
        {
            var result = Clamp(new Rect(-300, 100, 610, 430), Primary);

            Assert.Equal(new Rect(0, 100, 610, 430), result);
        }

        [Fact]
        public void WindowPartiallyOffTheTopEdgeIsMovedBackInside()
        {
            var result = Clamp(new Rect(100, -200, 610, 430), Primary);

            Assert.Equal(new Rect(100, 0, 610, 430), result);
        }

        [Fact]
        public void WindowPartiallyOffTheBottomRightCornerIsMovedBackInside()
        {
            var result = Clamp(new Rect(1700, 800, 610, 430), Primary);

            Assert.Equal(new Rect(1310, 610, 610, 430), result);
        }

        [Fact]
        public void WindowLargerThanTheVirtualScreenIsShrunkToIt()
        {
            var result = Clamp(new Rect(-50, -50, 3000, 2000), Primary);

            Assert.Equal(Primary, result);
        }

        [Fact]
        public void WindowLargerThanASingleMonitorMayUseTheWholeVirtualScreen()
        {
            var right = new Rect(1920, 0, 1920, 1040);

            var result = Clamp(new Rect(100, 100, 3500, 900), Primary, right);

            Assert.Equal(new Rect(100, 100, 3500, 900), result);
        }

        [Fact]
        public void WindowLargerThanTheVirtualScreenOfTwoMonitorsIsShrunkToIt()
        {
            var right = new Rect(1920, 0, 1920, 1040);

            var result = Clamp(new Rect(-50, 20, 5000, 2000), Primary, right);

            Assert.Equal(new Rect(0, 0, 3840, 1040), result);
        }

        [Fact]
        public void WindowSmallerThanTheMinimumSizeIsEnlarged()
        {
            var result = Clamp(new Rect(100, 100, 30, 20), Primary);

            Assert.Equal(new Rect(100, 100, 610, 430), result);
        }

        [Fact]
        public void WindowWithoutAValidSizeGetsTheMinimumSize()
        {
            var result = Clamp(new Rect(100, 100, double.NaN, 0), Primary);

            Assert.Equal(new Rect(100, 100, 610, 430), result);
        }

        [Fact]
        public void MinimumSizeWinsWhenTheVirtualScreenIsSmaller()
        {
            var tiny = new Rect(0, 0, 500, 400);

            var result = WindowBoundsClamper.Clamp(new Rect(20, 20, 610, 430), MinimumSize, new[] { tiny }, tiny);

            Assert.Equal(new Rect(0, 0, 610, 430), result);
        }

        [Fact]
        public void WindowOnASecondaryMonitorWithNegativeCoordinatesIsNotChanged()
        {
            var left = new Rect(-1920, 0, 1920, 1040);
            var desired = new Rect(-1500, 200, 610, 430);

            Assert.Equal(desired, Clamp(desired, left, Primary));
        }

        [Fact]
        public void WindowStraddlingTwoMonitorsIsNotChanged()
        {
            var left = new Rect(-1920, 0, 1920, 1040);
            var desired = new Rect(-300, 200, 610, 430);

            Assert.Equal(desired, Clamp(desired, left, Primary));
        }

        [Fact]
        public void WindowPartiallyBeyondTheNegativeEdgeOfTheVirtualScreenIsMovedBackInside()
        {
            var left = new Rect(-1920, 0, 1920, 1040);

            var result = Clamp(new Rect(-2100, 200, 610, 430), left, Primary);

            Assert.Equal(new Rect(-1920, 200, 610, 430), result);
        }

        [Fact]
        public void WindowFarBeyondTheNegativeEdgeIsCentredOnThePrimaryMonitorNotOnTheSecondary()
        {
            var left = new Rect(-1920, 0, 1920, 1040);

            var result = Clamp(new Rect(-6000, 200, 610, 430), left, Primary);

            Assert.Equal(CentredOnPrimary, result);
        }

        [Fact]
        public void WindowAboveAMonitorPlacedOnTopOfThePrimaryOneIsMovedBackInside()
        {
            var top = new Rect(0, -1040, 1920, 1040);

            var result = Clamp(new Rect(100, -1300, 610, 430), top, Primary);

            Assert.Equal(new Rect(100, -1040, 610, 430), result);
        }

        [Fact]
        public void PrimaryMonitorWithNegativeOriginIsUsedForCentring()
        {
            // The primary monitor always has the origin (0, 0) in Windows, but it can be anywhere in our coordinate space.
            var secondary = new Rect(0, 0, 1000, 800);
            var primary = new Rect(-1000, 0, 1000, 800);

            var result = WindowBoundsClamper.Clamp(new Rect(9000, 9000, 400, 300), new Size(400, 300), new[] { secondary, primary }, primary);

            Assert.Equal(new Rect(-700, 250, 400, 300), result);
        }

        [Fact]
        public void WindowInTheUnusedCornerOfTheVirtualScreenIsCentredOnThePrimaryMonitor()
        {
            // Monitors of different heights: the virtual screen contains an area below the small monitor that is on no monitor.
            var big = new Rect(0, 0, 1920, 1040);
            var small = new Rect(1920, 0, 1280, 600);

            var result = Clamp(new Rect(2500, 700, 610, 430), big, small);

            Assert.Equal(CentredOnPrimary, result);
        }

        [Fact]
        public void WindowPartlyOnTheSmallerMonitorIsNotMovedWhileItIsInsideTheVirtualScreen()
        {
            var big = new Rect(0, 0, 1920, 1040);
            var small = new Rect(1920, 0, 1280, 600);
            var desired = new Rect(2500, 300, 610, 430);

            Assert.Equal(desired, Clamp(desired, big, small));
        }

        [Fact]
        public void UnusableWorkAreasAreIgnored()
        {
            var desired = new Rect(5000, 100, 610, 430);

            var result = Clamp(desired, Rect.Empty, new Rect(0, 0, 0, 0), Primary);

            Assert.Equal(CentredOnPrimary, result);
        }

        [Fact]
        public void BoundsAreReturnedUnchangedWhenNoMonitorIsKnown()
        {
            var desired = new Rect(5000, 100, 610, 430);

            Assert.Equal(desired, Clamp(desired));
        }

        [Fact]
        public void InvalidPrimaryWorkAreaFallsBackToTheFirstMonitor()
        {
            var result = WindowBoundsClamper.Clamp(new Rect(5000, 100, 610, 430), MinimumSize, new[] { Primary }, Rect.Empty);

            Assert.Equal(CentredOnPrimary, result);
        }

        [Fact]
        public void MissingWorkAreasAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => WindowBoundsClamper.Clamp(new Rect(0, 0, 610, 430), MinimumSize, null, Primary));
        }
    }
}
