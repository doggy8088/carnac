using Carnac.Logic;
using Carnac.Logic.Overlay;
using Xunit;

namespace Carnac.Tests.Overlay
{
    public class OverlayWindowStylesFacts
    {
        const int WsExLayered = 0x00080000;
        const int WsExTopmost = 0x00000008;

        public class by_default
        {
            [Fact]
            public void the_overlay_is_click_through_hidden_from_window_lists_and_never_activated()
            {
                var style = OverlayWindowStyles.Apply(0, false);

                Assert.Equal(Win32Methods.WS_EX_TRANSPARENT, style & Win32Methods.WS_EX_TRANSPARENT);
                Assert.Equal(Win32Methods.WS_EX_TOOLWINDOW, style & Win32Methods.WS_EX_TOOLWINDOW);
                Assert.Equal(Win32Methods.WS_EX_NOACTIVATE, style & Win32Methods.WS_EX_NOACTIVATE);
            }
        }

        public class when_capture_friendly
        {
            [Fact]
            public void the_overlay_is_not_a_tool_window_so_capture_tools_list_it()
            {
                var style = OverlayWindowStyles.Apply(0, true);

                Assert.Equal(0, style & Win32Methods.WS_EX_TOOLWINDOW);
            }

            [Fact]
            public void the_overlay_stays_click_through_and_never_activated()
            {
                var style = OverlayWindowStyles.Apply(0, true);

                Assert.Equal(Win32Methods.WS_EX_TRANSPARENT, style & Win32Methods.WS_EX_TRANSPARENT);
                Assert.Equal(Win32Methods.WS_EX_NOACTIVATE, style & Win32Methods.WS_EX_NOACTIVATE);
            }

            [Fact]
            public void a_tool_window_style_that_is_already_set_is_removed()
            {
                var toolWindow = OverlayWindowStyles.Apply(WsExLayered, false);

                var style = OverlayWindowStyles.Apply(toolWindow, true);

                Assert.Equal(0, style & Win32Methods.WS_EX_TOOLWINDOW);
                Assert.Equal(WsExLayered, style & WsExLayered);
            }

            [Fact]
            public void switching_back_makes_it_a_tool_window_again()
            {
                var friendly = OverlayWindowStyles.Apply(WsExLayered, true);

                var style = OverlayWindowStyles.Apply(friendly, false);

                Assert.Equal(Win32Methods.WS_EX_TOOLWINDOW, style & Win32Methods.WS_EX_TOOLWINDOW);
            }
        }

        public class in_both_modes
        {
            [Fact]
            public void the_styles_the_window_already_has_are_kept()
            {
                foreach (var captureFriendly in new[] { false, true })
                {
                    var style = OverlayWindowStyles.Apply(WsExLayered | WsExTopmost, captureFriendly);

                    Assert.Equal(WsExLayered, style & WsExLayered);
                    Assert.Equal(WsExTopmost, style & WsExTopmost);
                }
            }

            [Fact]
            public void applying_the_styles_twice_changes_nothing()
            {
                foreach (var captureFriendly in new[] { false, true })
                {
                    var once = OverlayWindowStyles.Apply(WsExLayered, captureFriendly);

                    Assert.Equal(once, OverlayWindowStyles.Apply(once, captureFriendly));
                }
            }

            [Fact]
            public void the_two_modes_differ_in_the_tool_window_style_only()
            {
                var hidden = OverlayWindowStyles.Apply(WsExLayered, false);
                var friendly = OverlayWindowStyles.Apply(WsExLayered, true);

                Assert.Equal(Win32Methods.WS_EX_TOOLWINDOW, hidden ^ friendly);
            }

            [Fact]
            public void the_no_activate_style_has_the_documented_value()
            {
                Assert.Equal(0x08000000, Win32Methods.WS_EX_NOACTIVATE);
            }
        }

        public class when_a_setting_changes
        {
            [Fact]
            public void the_capture_setting_applies_the_styles_again()
            {
                Assert.True(OverlayWindowStyles.AffectsStyles("CaptureFriendlyWindow"));
            }

            [Fact]
            public void a_change_of_everything_applies_the_styles_again()
            {
                Assert.True(OverlayWindowStyles.AffectsStyles(null));
                Assert.True(OverlayWindowStyles.AffectsStyles(string.Empty));
            }

            [Fact]
            public void other_settings_leave_the_styles_alone()
            {
                foreach (var name in new[] { "Screen", "Placement", "ItemMaxWidth", "FontSize", "AutoUpdate", "captureFriendlyWindow" })
                    Assert.False(OverlayWindowStyles.AffectsStyles(name), name);
            }
        }

        public class the_window_title
        {
            [Fact]
            public void is_stable_so_capture_tools_can_select_the_window_by_title()
            {
                Assert.Equal("Carnac Overlay", OverlayWindowStyles.WindowTitle);
            }
        }
    }
}
