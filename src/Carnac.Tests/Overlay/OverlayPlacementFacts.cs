using System;
using System.Collections.Generic;
using Carnac.Logic.Enums;
using Carnac.Logic.Models;
using Carnac.Logic.Native;
using Carnac.Logic.Overlay;
using SettingsProviderNet;
using Xunit;

namespace Carnac.Tests.Overlay
{
    public class OverlayPlacementFacts
    {
        static readonly NotificationPlacement[] AllPlacements =
        {
            NotificationPlacement.TopLeft,
            NotificationPlacement.BottomLeft,
            NotificationPlacement.TopRight,
            NotificationPlacement.BottomRight
        };

        static readonly PixelRect Full = new PixelRect(0, 0, 1920, 1080);

        static DetailedScreen Screen(int index, int left, int top, int width, int height)
        {
            return new DetailedScreen { Index = index, Left = left, Top = top, Width = width, Height = height };
        }

        public class when_calculating_the_window_rectangle
        {
            [Fact]
            public void a_left_placement_is_anchored_to_the_left_edge_with_a_gap()
            {
                var rect = OverlayPlacement.Calculate(Full, NotificationPlacement.TopLeft, 500, 1.0);

                Assert.Equal(new PixelRect(1, 1, 500, 1078), rect);
            }

            [Fact]
            public void a_right_placement_is_anchored_to_the_right_edge_with_a_gap()
            {
                var rect = OverlayPlacement.Calculate(Full, NotificationPlacement.TopRight, 500, 1.0);

                Assert.Equal(new PixelRect(1419, 1, 500, 1078), rect);
                Assert.Equal(1919, rect.Right);
            }

            [Fact]
            public void the_window_is_the_same_for_the_top_and_the_bottom_placement_of_a_side()
            {
                Assert.Equal(
                    OverlayPlacement.Calculate(Full, NotificationPlacement.TopLeft, 500, 1.0),
                    OverlayPlacement.Calculate(Full, NotificationPlacement.BottomLeft, 500, 1.0));
                Assert.Equal(
                    OverlayPlacement.Calculate(Full, NotificationPlacement.TopRight, 500, 1.0),
                    OverlayPlacement.Calculate(Full, NotificationPlacement.BottomRight, 500, 1.0));
            }

            [Fact]
            public void an_unset_placement_is_anchored_right_like_the_popup_list_inside_the_window()
            {
                var rect = OverlayPlacement.Calculate(Full, (NotificationPlacement)0, 500, 1.0);

                Assert.Equal(1419, rect.X);
            }

            [Fact]
            public void the_width_is_scaled_by_the_dpi_and_rounded_up_to_whole_pixels()
            {
                Assert.Equal(624, OverlayPlacement.Calculate(Full, NotificationPlacement.TopLeft, 416, 1.5).Width);
                Assert.Equal(522, OverlayPlacement.Calculate(Full, NotificationPlacement.TopLeft, 417, 1.25).Width);
                Assert.Equal(832, OverlayPlacement.Calculate(Full, NotificationPlacement.TopLeft, 416, 2.0).Width);
            }

            [Fact]
            public void a_dpi_scale_that_is_not_positive_is_treated_as_100_percent()
            {
                Assert.Equal(500, OverlayPlacement.Calculate(Full, NotificationPlacement.TopLeft, 500, 0).Width);
                Assert.Equal(500, OverlayPlacement.Calculate(Full, NotificationPlacement.TopLeft, 500, -2).Width);
                Assert.Equal(500, OverlayPlacement.Calculate(Full, NotificationPlacement.TopLeft, 500, double.NaN).Width);
            }

            [Fact]
            public void content_wider_than_the_monitor_is_clamped_to_the_monitor_minus_the_gaps()
            {
                var left = OverlayPlacement.Calculate(Full, NotificationPlacement.TopLeft, 5000, 1.0);
                var right = OverlayPlacement.Calculate(Full, NotificationPlacement.TopRight, 5000, 1.0);

                Assert.Equal(new PixelRect(1, 1, 1918, 1078), left);
                Assert.Equal(left, right);
            }

            [Fact]
            public void content_that_is_empty_or_not_a_number_still_gives_a_window_one_pixel_wide()
            {
                Assert.Equal(1, OverlayPlacement.Calculate(Full, NotificationPlacement.TopLeft, 0, 1.0).Width);
                Assert.Equal(1, OverlayPlacement.Calculate(Full, NotificationPlacement.TopLeft, -10, 1.0).Width);
                Assert.Equal(1, OverlayPlacement.Calculate(Full, NotificationPlacement.TopLeft, double.NaN, 1.0).Width);
            }

            [Fact]
            public void infinite_content_is_clamped_to_the_monitor_like_any_other_content_that_is_too_wide()
            {
                Assert.Equal(1918, OverlayPlacement.Calculate(Full, NotificationPlacement.TopLeft, double.PositiveInfinity, 1.0).Width);
                Assert.Equal(1918, OverlayPlacement.Calculate(Full, NotificationPlacement.TopLeft, 1e30, 1.0).Width);
            }

            [Fact]
            public void a_monitor_below_the_primary_monitor_keeps_its_vertical_origin()
            {
                var below = new PixelRect(0, 1080, 1920, 1080);

                Assert.Equal(new PixelRect(1, 1081, 500, 1078), OverlayPlacement.Calculate(below, NotificationPlacement.BottomLeft, 500, 1.0));
                Assert.Equal(new PixelRect(1419, 1081, 500, 1078), OverlayPlacement.Calculate(below, NotificationPlacement.BottomRight, 500, 1.0));
            }

            [Fact]
            public void a_monitor_above_the_primary_monitor_has_a_negative_vertical_origin()
            {
                var above = new PixelRect(0, -1080, 1920, 1080);

                Assert.Equal(new PixelRect(1, -1079, 500, 1078), OverlayPlacement.Calculate(above, NotificationPlacement.TopLeft, 500, 1.0));
            }

            [Fact]
            public void a_monitor_left_of_the_primary_monitor_has_a_negative_horizontal_origin()
            {
                var leftOfPrimary = new PixelRect(-1920, 0, 1920, 1080);

                var left = OverlayPlacement.Calculate(leftOfPrimary, NotificationPlacement.TopLeft, 500, 1.0);
                var right = OverlayPlacement.Calculate(leftOfPrimary, NotificationPlacement.TopRight, 500, 1.0);

                Assert.Equal(new PixelRect(-1919, 1, 500, 1078), left);
                Assert.Equal(new PixelRect(-501, 1, 500, 1078), right);
                Assert.Equal(-1, right.Right);
            }

            [Fact]
            public void monitors_of_different_sizes_are_placed_relative_to_their_own_rectangle()
            {
                var big = new PixelRect(1920, -360, 2560, 1440);
                var small = new PixelRect(-1280, 200, 1280, 720);

                Assert.Equal(new PixelRect(1921, -359, 600, 1438), OverlayPlacement.Calculate(big, NotificationPlacement.TopLeft, 600, 1.0));
                Assert.Equal(new PixelRect(3879, -359, 600, 1438), OverlayPlacement.Calculate(big, NotificationPlacement.BottomRight, 600, 1.0));
                Assert.Equal(new PixelRect(-1279, 201, 600, 718), OverlayPlacement.Calculate(small, NotificationPlacement.TopLeft, 600, 1.0));
                Assert.Equal(new PixelRect(-601, 201, 600, 718), OverlayPlacement.Calculate(small, NotificationPlacement.TopRight, 600, 1.0));
            }

            [Fact]
            public void the_window_never_covers_its_monitor_in_any_placement_layout_or_size()
            {
                var monitors = new[]
                {
                    Full,
                    new PixelRect(0, 1080, 1920, 1080),
                    new PixelRect(-1920, 0, 1920, 1080),
                    new PixelRect(3840, -720, 2560, 1440),
                    new PixelRect(0, 0, 800, 600),
                    new PixelRect(-1024, -768, 1024, 768)
                };
                var contentWidths = new[] { 1.0, 100.0, 416.0, 900.0, 1920.0, 4000.0, 1e9 };
                var scales = new[] { 1.0, 1.25, 1.5, 2.0, 3.0 };

                foreach (var monitor in monitors)
                foreach (var placement in AllPlacements)
                foreach (var contentWidth in contentWidths)
                foreach (var scale in scales)
                {
                    var rect = OverlayPlacement.Calculate(monitor, placement, contentWidth, scale);
                    var message = string.Format("monitor {0}, {1}, content {2}, scale {3} -> {4}", monitor, placement, contentWidth, scale, rect);

                    Assert.True(rect.X >= monitor.X + OverlayPlacement.EdgeGapPixels, message);
                    Assert.True(rect.Y >= monitor.Y + OverlayPlacement.EdgeGapPixels, message);
                    Assert.True(rect.Right <= monitor.Right - OverlayPlacement.EdgeGapPixels, message);
                    Assert.True(rect.Bottom <= monitor.Bottom - OverlayPlacement.EdgeGapPixels, message);
                    Assert.True(rect.Width >= 1 && rect.Height >= 1, message);
                }
            }

            [Fact]
            public void the_default_popup_width_leaves_most_of_the_monitor_uncovered()
            {
                var width = OverlayPlacement.GetContentWidth(350, 0, 0);

                var rect = OverlayPlacement.Calculate(Full, NotificationPlacement.BottomLeft, width, 1.0);

                Assert.True(rect.Width < Full.Width / 2, rect.ToString());
            }
        }

        public class when_calculating_the_width_the_popups_need
        {
            [Fact]
            public void it_is_the_widest_popup_plus_the_icon_the_chrome_and_both_offsets()
            {
                var width = OverlayPlacement.GetContentWidth(350, 20, 30);

                Assert.Equal(350 + OverlayPlacement.IconWidth + OverlayPlacement.ItemChromeWidth + 20 + 30, width);
            }

            [Fact]
            public void negative_values_do_not_shrink_the_width()
            {
                var width = OverlayPlacement.GetContentWidth(-5, -20, -30);

                Assert.Equal(OverlayPlacement.IconWidth + OverlayPlacement.ItemChromeWidth, width);
            }
        }

        public class when_deciding_which_side_the_window_is_anchored_to
        {
            [Fact]
            public void it_agrees_with_the_alignment_of_the_popup_list_for_every_placement()
            {
                var placements = new List<NotificationPlacement>(AllPlacements) { (NotificationPlacement)0 };

                foreach (var placement in placements)
                {
                    var settings = new PopupSettings { Placement = placement };

                    Assert.Equal(settings.Alignment == "Left", OverlayPlacement.IsLeftAligned(placement));
                }
            }
        }

        public class when_selecting_the_screen
        {
            [Fact]
            public void the_screen_with_the_configured_index_wins()
            {
                var screens = new[] { Screen(1, 0, 0, 1920, 1080), Screen(2, 1920, 0, 1920, 1080), Screen(3, 0, 1080, 1920, 1080) };

                Assert.Same(screens[2], OverlayPlacement.SelectScreen(screens, 3));
            }

            [Fact]
            public void an_unknown_index_falls_back_to_the_primary_screen()
            {
                var screens = new[] { Screen(1, -1920, 0, 1920, 1080), Screen(2, 0, 0, 2560, 1440), Screen(3, 2560, 0, 1920, 1080) };

                Assert.Same(screens[1], OverlayPlacement.SelectScreen(screens, 0));
                Assert.Same(screens[1], OverlayPlacement.SelectScreen(screens, 9));
            }

            [Fact]
            public void without_a_screen_at_the_origin_the_first_screen_is_used()
            {
                var screens = new[] { Screen(1, 100, 100, 800, 600), Screen(2, 900, 100, 800, 600) };

                Assert.Same(screens[0], OverlayPlacement.SelectScreen(screens, 7));
            }

            [Fact]
            public void no_screens_means_no_screen()
            {
                Assert.Null(OverlayPlacement.SelectScreen(new DetailedScreen[0], 1));
            }

            [Fact]
            public void null_screens_in_the_list_are_ignored()
            {
                var screens = new[] { null, Screen(1, 0, 0, 800, 600) };

                Assert.Same(screens[1], OverlayPlacement.SelectScreen(screens, 4));
            }

            [Fact]
            public void a_null_list_is_rejected()
            {
                Assert.Throws<ArgumentNullException>(() => OverlayPlacement.SelectScreen(null, 1));
            }
        }

        public class when_resolving_the_window_for_the_settings
        {
            [Fact]
            public void the_selected_screen_the_placement_the_width_and_the_offsets_are_used()
            {
                var screens = new[] { Screen(1, 0, 0, 1920, 1080), Screen(2, 0, 1080, 2560, 1440) };
                var settings = new PopupSettings
                {
                    Screen = 2,
                    Placement = NotificationPlacement.BottomRight,
                    ItemMaxWidth = 350,
                    LeftOffset = 10,
                    RightOffset = 40,
                    TopOffset = 300,
                    BottomOffset = 300
                };
                var expectedWidth = 350 + OverlayPlacement.IconWidth + OverlayPlacement.ItemChromeWidth + 10 + 40;

                var rect = OverlayPlacement.Resolve(screens, settings, 1.0);

                Assert.Equal(new PixelRect(2560 - 1 - expectedWidth, 1081, expectedWidth, 1438), rect.Value);
            }

            [Fact]
            public void the_top_and_bottom_offsets_do_not_change_the_window_because_the_popup_list_applies_them()
            {
                var screens = new[] { Screen(1, 0, 0, 1920, 1080) };
                var plain = new PopupSettings { Screen = 1, Placement = NotificationPlacement.TopLeft, ItemMaxWidth = 350 };
                var offset = new PopupSettings { Screen = 1, Placement = NotificationPlacement.TopLeft, ItemMaxWidth = 350, TopOffset = 200, BottomOffset = 300 };

                Assert.Equal(OverlayPlacement.Resolve(screens, plain, 1.0), OverlayPlacement.Resolve(screens, offset, 1.0));
            }

            [Fact]
            public void the_dpi_scale_is_applied_to_the_width()
            {
                var screens = new[] { Screen(1, 0, 0, 3840, 2160) };
                var settings = new PopupSettings { Screen = 1, Placement = NotificationPlacement.TopLeft, ItemMaxWidth = 350 };

                var at100 = OverlayPlacement.Resolve(screens, settings, 1.0).Value;
                var at200 = OverlayPlacement.Resolve(screens, settings, 2.0).Value;

                Assert.Equal(at100.Width * 2, at200.Width);
            }

            [Fact]
            public void an_unknown_screen_falls_back_to_the_primary_screen()
            {
                var screens = new[] { Screen(1, -1920, 0, 1920, 1080), Screen(2, 0, 0, 1920, 1080) };
                var settings = new PopupSettings { Screen = 5, Placement = NotificationPlacement.TopLeft, ItemMaxWidth = 350 };

                var rect = OverlayPlacement.Resolve(screens, settings, 1.0).Value;

                Assert.Equal(1, rect.X);
            }

            [Fact]
            public void without_any_screen_there_is_no_window_rectangle()
            {
                Assert.False(OverlayPlacement.Resolve(new DetailedScreen[0], new PopupSettings(), 1.0).HasValue);
            }

            [Fact]
            public void null_arguments_are_rejected()
            {
                Assert.Throws<ArgumentNullException>(() => OverlayPlacement.Resolve(null, new PopupSettings(), 1.0));
                Assert.Throws<ArgumentNullException>(() => OverlayPlacement.Resolve(new DetailedScreen[0], null, 1.0));
            }
        }

        public class when_converting_a_screen_to_pixels
        {
            [Fact]
            public void the_screen_values_are_rounded_to_whole_pixels()
            {
                var rect = OverlayPlacement.ToPixelRect(new DetailedScreen { Left = -1920.4, Top = 1079.6, Width = 1920.2, Height = 1080.6 });

                Assert.Equal(new PixelRect(-1920, 1080, 1920, 1081), rect);
            }

            [Fact]
            public void a_null_screen_is_rejected()
            {
                Assert.Throws<ArgumentNullException>(() => OverlayPlacement.ToPixelRect(null));
            }
        }

        public class when_the_settings_come_from_the_settings_provider
        {
            class InMemoryStorage : ISettingsStorage
            {
                public readonly Dictionary<string, string> Values = new Dictionary<string, string>();

                public void Save(string name, Dictionary<string, string> values)
                {
                    foreach (var pair in values)
                        Values[pair.Key] = pair.Value;
                }

                public Dictionary<string, string> Load(string name)
                {
                    return new Dictionary<string, string>(Values);
                }
            }

            [Fact]
            public void the_provider_hands_out_one_shared_instance_so_the_overlay_sees_what_preferences_changes()
            {
                var provider = new SettingsProvider(new InMemoryStorage());

                Assert.Same(provider.GetSettings<PopupSettings>(), provider.GetSettings<PopupSettings>());
            }

            [Fact]
            public void settings_saved_before_the_left_and_top_origin_was_removed_still_load()
            {
                var storage = new InMemoryStorage();
                storage.Values["Screen"] = "2";
                storage.Values["Left"] = "1920";
                storage.Values["Top"] = "0";

                var settings = new SettingsProvider(storage).GetSettings<PopupSettings>();

                Assert.Equal(2, settings.Screen);
            }
        }

        public class when_a_setting_changes
        {
            [Fact]
            public void the_settings_that_decide_the_window_place_it_again()
            {
                foreach (var name in new[] { "Screen", "Placement", "ItemMaxWidth", "LeftOffset", "RightOffset" })
                    Assert.True(OverlayPlacement.AffectsPlacement(name), name);
            }

            [Fact]
            public void the_real_settings_raise_property_changed_under_the_names_that_are_listened_for()
            {
                var settings = new PopupSettings();
                var raised = new List<string>();
                settings.PropertyChanged += (sender, e) => raised.Add(e.PropertyName);

                settings.Screen = 2;
                settings.Placement = NotificationPlacement.TopRight;
                settings.ItemMaxWidth = 500;
                settings.LeftOffset = 30;
                settings.RightOffset = 40;

                foreach (var name in new[] { "Screen", "Placement", "ItemMaxWidth", "LeftOffset", "RightOffset" })
                {
                    Assert.True(raised.Contains(name), name + " raised no change notification");
                    Assert.True(OverlayPlacement.AffectsPlacement(name), name);
                }
            }

            [Fact]
            public void every_setting_changing_at_once_places_it_again()
            {
                Assert.True(OverlayPlacement.AffectsPlacement(null));
                Assert.True(OverlayPlacement.AffectsPlacement(string.Empty));
            }

            [Fact]
            public void other_settings_leave_the_window_alone()
            {
                foreach (var name in new[] { "TopOffset", "BottomOffset", "Margins", "FontSize", "ItemOpacity", "ItemBackgroundColor", "ShowApplicationIcon", "screen" })
                    Assert.False(OverlayPlacement.AffectsPlacement(name), name);
            }
        }
    }
}
