using System;
using System.Collections.Generic;
using Carnac.Logic.Native;
using Carnac.Logic.Overlay;
using Xunit;

namespace Carnac.Tests
{
    public class ScreenListFacts
    {
        static DisplayInfo Display(string deviceName, int left, int top, int width, int height, bool primary = false)
        {
            return new DisplayInfo
            {
                DeviceName = deviceName,
                MonitorName = "Monitor of " + deviceName,
                IsPrimary = primary,
                Left = left,
                Top = top,
                Width = width,
                Height = height
            };
        }

        static IEnumerable<KeyValuePair<string, PixelRect>> Bounds(params object[] pairs)
        {
            var bounds = new List<KeyValuePair<string, PixelRect>>();
            for (var i = 0; i < pairs.Length; i += 2)
                bounds.Add(new KeyValuePair<string, PixelRect>((string)pairs[i], (PixelRect)pairs[i + 1]));
            return bounds;
        }

        public class when_building_the_list_of_screens
        {
            [Fact]
            public void every_display_becomes_a_screen_with_its_name_position_and_size()
            {
                var screens = ScreenList.Build(new[] { Display(@"\\.\DISPLAY1", -1920, 200, 1920, 1080, true) }, null);

                Assert.Equal(1, screens.Count);
                Assert.Equal(1, screens[0].Index);
                Assert.Equal(@"\\.\DISPLAY1", screens[0].DeviceName);
                Assert.Equal(@"Monitor of \\.\DISPLAY1", screens[0].FriendlyName);
                Assert.True(screens[0].IsPrimary);
                Assert.Equal(-1920, screens[0].Left);
                Assert.Equal(200, screens[0].Top);
                Assert.Equal(1920, screens[0].Width);
                Assert.Equal(1080, screens[0].Height);
            }

            [Fact]
            public void the_numbers_follow_the_enumeration_order_not_the_position()
            {
                var displays = new[]
                {
                    Display(@"\\.\DISPLAY1", 0, 0, 1920, 1080, true),
                    Display(@"\\.\DISPLAY2", -1920, 0, 1920, 1080),
                    Display(@"\\.\DISPLAY3", 1920, 0, 1920, 1080)
                };

                var screens = ScreenList.Build(displays, null);

                Assert.Equal(new[] { @"\\.\DISPLAY1", @"\\.\DISPLAY2", @"\\.\DISPLAY3" }, screens.ConvertAll(s => s.DeviceName).ToArray());
                Assert.Equal(new[] { 1, 2, 3 }, screens.ConvertAll(s => s.Index).ToArray());
            }

            [Fact]
            public void a_display_without_a_mode_is_skipped_but_keeps_its_number()
            {
                var displays = new[]
                {
                    Display(@"\\.\DISPLAY1", 0, 0, 1920, 1080, true),
                    Display(@"\\.\DISPLAY2", 0, 0, 0, 0),
                    Display(@"\\.\DISPLAY3", 1920, 0, 1920, 1080)
                };

                var screens = ScreenList.Build(displays, null);

                Assert.Equal(new[] { 1, 3 }, screens.ConvertAll(s => s.Index).ToArray());
            }

            [Fact]
            public void the_monitor_rectangle_of_the_window_apis_is_found_by_device_name_ignoring_case()
            {
                var displays = new[] { Display(@"\\.\DISPLAY1", 0, 0, 3840, 2160, true), Display(@"\\.\DISPLAY2", 3840, 0, 1920, 1080) };
                var bounds = Bounds(@"\\.\display2", new PixelRect(3840, 0, 2880, 1620), @"\\.\DISPLAY1", new PixelRect(0, 0, 3840, 2160));

                var screens = ScreenList.Build(displays, bounds);

                Assert.Equal(new PixelRect(0, 0, 3840, 2160), screens[0].Bounds.Value);
                Assert.Equal(new PixelRect(3840, 0, 2880, 1620), screens[1].Bounds.Value);
                Assert.Equal(1920, screens[1].Width);
            }

            [Fact]
            public void a_screen_without_a_reported_monitor_rectangle_has_none()
            {
                var displays = new[] { Display(@"\\.\DISPLAY1", 0, 0, 1920, 1080, true), Display(@"\\.\DISPLAY2", 1920, 0, 1920, 1080) };

                var withOther = ScreenList.Build(displays, Bounds("DISPLAY", new PixelRect(0, 0, 1920, 1080)));
                var withNone = ScreenList.Build(displays, null);

                Assert.False(withOther[0].Bounds.HasValue);
                Assert.False(withOther[1].Bounds.HasValue);
                Assert.False(withNone[0].Bounds.HasValue);
            }

            [Fact]
            public void null_displays_are_ignored()
            {
                var screens = ScreenList.Build(new[] { null, Display(@"\\.\DISPLAY1", 0, 0, 1920, 1080, true) }, null);

                Assert.Equal(1, screens.Count);
            }

            [Fact]
            public void no_displays_give_no_screens()
            {
                Assert.Equal(0, ScreenList.Build(new DisplayInfo[0], null).Count);
            }

            [Fact]
            public void a_null_list_of_displays_is_rejected()
            {
                Assert.Throws<ArgumentNullException>(() => ScreenList.Build(null, null));
            }
        }

        public class when_describing_a_screen
        {
            [Fact]
            public void the_description_starts_with_the_number_that_is_drawn_on_the_tile()
            {
                var screen = new DetailedScreen { Index = 2, FriendlyName = "DELL U2415", Width = 1920, Height = 1200 };

                Assert.Equal("2: DELL U2415 (1920 x 1200)", screen.Description);
            }

            [Fact]
            public void the_primary_screen_says_so()
            {
                var screen = new DetailedScreen { Index = 1, FriendlyName = "Generic PnP Monitor", Width = 2560, Height = 1440, IsPrimary = true };

                Assert.Equal("1: Generic PnP Monitor (2560 x 1440, primary)", screen.Description);
            }
        }
    }
}
