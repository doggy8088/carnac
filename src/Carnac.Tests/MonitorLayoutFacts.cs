using System;
using System.Collections.Generic;
using System.Linq;
using Carnac.Logic.Native;
using Xunit;

namespace Carnac.Tests
{
    public class MonitorLayoutFacts
    {
        const int Precision = 6;

        static DetailedScreen Screen(int index, int left, int top, int width, int height)
        {
            return new DetailedScreen { Index = index, Left = left, Top = top, Width = width, Height = height };
        }

        static void AssertFitsTheLimits(MonitorLayout layout, IList<DetailedScreen> screens)
        {
            Assert.True(layout.Width <= MonitorLayout.MaxWidth + 1e-9, "width " + layout.Width);
            Assert.True(layout.Height <= MonitorLayout.MaxHeight + 1e-9, "height " + layout.Height);
            foreach (var screen in screens)
            {
                Assert.True(screen.RelativeWidth <= MonitorLayout.MaxTileWidth + 1e-9, "tile width " + screen.RelativeWidth);
                Assert.True(screen.LayoutLeft + screen.RelativeWidth <= layout.Width + 1e-9);
                Assert.True(screen.LayoutTop + screen.RelativeHeight <= layout.Height + 1e-9);
                Assert.True(screen.LayoutLeft >= 0 && screen.LayoutTop >= 0);
            }
        }

        public class when_the_screens_are_side_by_side
        {
            [Fact]
            public void they_are_drawn_next_to_each_other_in_the_order_of_their_positions()
            {
                var screens = new List<DetailedScreen>
                {
                    Screen(1, 1920, 0, 1920, 1080),
                    Screen(2, 0, 0, 1920, 1080),
                    Screen(3, 3840, 0, 1920, 1080)
                };

                var layout = MonitorLayout.Create(screens);

                Assert.Equal(0, screens[1].LayoutLeft, Precision);
                Assert.Equal(screens[1].RelativeWidth, screens[0].LayoutLeft, Precision);
                Assert.Equal(screens[0].LayoutLeft + screens[0].RelativeWidth, screens[2].LayoutLeft, Precision);
                Assert.Equal(0, screens[0].LayoutTop, Precision);
                Assert.Equal(layout.Width, screens[2].LayoutLeft + screens[2].RelativeWidth, Precision);
                AssertFitsTheLimits(layout, screens);
            }

            [Fact]
            public void the_number_of_a_screen_does_not_decide_where_it_is_drawn()
            {
                var screens = new List<DetailedScreen>
                {
                    Screen(1, 0, 0, 1920, 1080),
                    Screen(2, 3840, 0, 1920, 1080),
                    Screen(3, 1920, 0, 1920, 1080)
                };

                MonitorLayout.Create(screens);

                Assert.True(screens[2].LayoutLeft > screens[0].LayoutLeft);
                Assert.True(screens[2].LayoutLeft < screens[1].LayoutLeft);
            }
        }

        public class when_the_screens_are_stacked
        {
            [Fact]
            public void a_screen_below_the_primary_screen_is_drawn_below_it()
            {
                var screens = new List<DetailedScreen> { Screen(1, 0, 0, 1920, 1080), Screen(2, 0, 1080, 1920, 1080) };

                var layout = MonitorLayout.Create(screens);

                Assert.Equal(0, screens[0].LayoutLeft, Precision);
                Assert.Equal(0, screens[1].LayoutLeft, Precision);
                Assert.Equal(0, screens[0].LayoutTop, Precision);
                Assert.Equal(screens[0].RelativeHeight, screens[1].LayoutTop, Precision);
                Assert.Equal(layout.Height, screens[1].LayoutTop + screens[1].RelativeHeight, Precision);
                AssertFitsTheLimits(layout, screens);
            }

            [Fact]
            public void a_screen_above_the_primary_screen_has_a_negative_origin_and_is_drawn_above_it()
            {
                var screens = new List<DetailedScreen> { Screen(1, 0, 0, 1920, 1080), Screen(2, 0, -1080, 1920, 1080) };

                var layout = MonitorLayout.Create(screens);

                Assert.Equal(0, screens[1].LayoutTop, Precision);
                Assert.Equal(screens[1].RelativeHeight, screens[0].LayoutTop, Precision);
                AssertFitsTheLimits(layout, screens);
            }

            [Fact]
            public void a_tall_arrangement_is_scaled_down_to_the_height_limit()
            {
                var screens = new List<DetailedScreen>
                {
                    Screen(1, 0, 0, 1920, 1080),
                    Screen(2, 0, 1080, 1920, 1080),
                    Screen(3, 0, 2160, 1920, 1080)
                };

                var layout = MonitorLayout.Create(screens);

                Assert.Equal(MonitorLayout.MaxHeight, layout.Height, Precision);
                Assert.True(screens[0].RelativeWidth < MonitorLayout.MaxTileWidth);
                AssertFitsTheLimits(layout, screens);
            }
        }

        public class when_the_arrangement_is_irregular
        {
            [Fact]
            public void an_l_shaped_arrangement_keeps_the_relative_positions()
            {
                var screens = new List<DetailedScreen>
                {
                    Screen(1, 0, 0, 1920, 1080),
                    Screen(2, 1920, 0, 1920, 1080),
                    Screen(3, 0, 1080, 1920, 1080)
                };

                var layout = MonitorLayout.Create(screens);

                Assert.Equal(0, screens[2].LayoutLeft, Precision);
                Assert.Equal(screens[0].RelativeHeight, screens[2].LayoutTop, Precision);
                Assert.Equal(screens[0].RelativeWidth, screens[1].LayoutLeft, Precision);
                Assert.Equal(0, screens[1].LayoutTop, Precision);
                AssertFitsTheLimits(layout, screens);
            }

            [Fact]
            public void screens_of_different_sizes_keep_their_size_ratio_and_the_widest_is_not_wider_than_the_tile_limit()
            {
                var screens = new List<DetailedScreen>
                {
                    Screen(1, 0, 0, 3840, 2160),
                    Screen(2, 3840, 540, 1920, 1080),
                    Screen(3, -1080, 0, 1080, 1920)
                };

                var layout = MonitorLayout.Create(screens);

                Assert.Equal(screens[0].RelativeWidth / 2, screens[1].RelativeWidth, Precision);
                Assert.Equal(screens[0].RelativeHeight / 2, screens[1].RelativeHeight, Precision);
                Assert.Equal(screens[0].RelativeWidth * 1080 / 3840, screens[2].RelativeWidth, Precision);
                Assert.Equal(screens[0].RelativeHeight / 4, screens[1].LayoutTop - screens[0].LayoutTop, Precision);
                AssertFitsTheLimits(layout, screens);
            }

            [Fact]
            public void the_widest_screen_of_a_short_arrangement_is_drawn_as_wide_as_the_tile_limit()
            {
                var screens = new List<DetailedScreen> { Screen(1, 0, 0, 1920, 1080), Screen(2, 1920, 0, 1280, 720) };

                MonitorLayout.Create(screens);

                Assert.Equal(MonitorLayout.MaxTileWidth, screens[0].RelativeWidth, Precision);
            }

            [Fact]
            public void the_size_ratio_of_a_screen_is_kept()
            {
                var screens = new List<DetailedScreen> { Screen(1, 0, 0, 1920, 1200) };

                MonitorLayout.Create(screens);

                Assert.Equal(1920.0 / 1200.0, screens[0].RelativeWidth / screens[0].RelativeHeight, Precision);
            }

            [Fact]
            public void the_arrangement_never_exceeds_the_limits()
            {
                var arrangements = new[]
                {
                    new List<DetailedScreen> { Screen(1, 0, 0, 800, 600) },
                    new List<DetailedScreen> { Screen(1, 0, 0, 3840, 2160), Screen(2, 3840, 0, 3840, 2160), Screen(3, 7680, 0, 3840, 2160), Screen(4, 11520, 0, 3840, 2160) },
                    new List<DetailedScreen> { Screen(1, 0, 0, 1080, 1920), Screen(2, 1080, 0, 1080, 1920), Screen(3, 2160, 0, 1080, 1920) },
                    new List<DetailedScreen> { Screen(1, 0, 0, 1920, 1080), Screen(2, -5000, -3000, 1920, 1080) }
                };

                foreach (var screens in arrangements)
                    AssertFitsTheLimits(MonitorLayout.Create(screens), screens);
            }
        }

        public class when_there_is_nothing_to_draw
        {
            [Fact]
            public void no_screens_give_an_empty_layout()
            {
                var layout = MonitorLayout.Create(new List<DetailedScreen>());

                Assert.Equal(0, layout.Width);
                Assert.Equal(0, layout.Height);
            }

            [Fact]
            public void screens_without_a_size_are_drawn_as_nothing()
            {
                var screens = new List<DetailedScreen> { Screen(1, 0, 0, 0, 0), Screen(2, 640, 480, 0, 0) };

                var layout = MonitorLayout.Create(screens);

                Assert.Equal(0, layout.Width);
                Assert.Equal(0, layout.Height);
                Assert.True(screens.All(s => s.RelativeWidth == 0 && s.RelativeHeight == 0 && s.LayoutLeft == 0 && s.LayoutTop == 0));
            }

            [Fact]
            public void a_screen_without_a_size_does_not_disturb_the_others()
            {
                var screens = new List<DetailedScreen> { Screen(1, 0, 0, 1920, 1080), Screen(2, 9999, 9999, 0, 0) };

                var layout = MonitorLayout.Create(screens);

                Assert.Equal(MonitorLayout.MaxTileWidth, layout.Width, Precision);
                Assert.Equal(0, screens[0].LayoutLeft, Precision);
                Assert.Equal(0, screens[1].RelativeWidth);
            }

            [Fact]
            public void earlier_results_are_replaced()
            {
                var screen = Screen(1, 0, 0, 1920, 1080);
                screen.RelativeWidth = 999;
                screen.LayoutLeft = 999;

                MonitorLayout.Create(new List<DetailedScreen> { screen });

                Assert.Equal(MonitorLayout.MaxTileWidth, screen.RelativeWidth, Precision);
                Assert.Equal(0, screen.LayoutLeft, Precision);
            }

            [Fact]
            public void null_screens_are_ignored_and_a_null_list_is_rejected()
            {
                var screens = new List<DetailedScreen> { null, Screen(1, 0, 0, 1920, 1080) };

                Assert.Equal(MonitorLayout.MaxTileWidth, MonitorLayout.Create(screens).Width, Precision);
                Assert.Throws<ArgumentNullException>(() => MonitorLayout.Create(null));
            }
        }
    }
}
