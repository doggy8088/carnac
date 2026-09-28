using Carnac.Logic;
using Carnac.Logic.Overlay;
using Xunit;

namespace Carnac.Tests.Overlay
{
    public class OverlayWindowStylesFacts
    {
        const int WsExLayered = 0x00080000;
        const int WsExTopmost = 0x00000008;

        [Fact]
        public void the_overlay_is_click_through_hidden_from_window_lists_and_never_activated()
        {
            var style = OverlayWindowStyles.Apply(0);

            Assert.Equal(Win32Methods.WS_EX_TRANSPARENT, style & Win32Methods.WS_EX_TRANSPARENT);
            Assert.Equal(Win32Methods.WS_EX_TOOLWINDOW, style & Win32Methods.WS_EX_TOOLWINDOW);
            Assert.Equal(Win32Methods.WS_EX_NOACTIVATE, style & Win32Methods.WS_EX_NOACTIVATE);
        }

        [Fact]
        public void the_styles_the_window_already_has_are_kept()
        {
            var style = OverlayWindowStyles.Apply(WsExLayered | WsExTopmost);

            Assert.Equal(WsExLayered, style & WsExLayered);
            Assert.Equal(WsExTopmost, style & WsExTopmost);
        }

        [Fact]
        public void applying_the_styles_twice_changes_nothing()
        {
            var once = OverlayWindowStyles.Apply(WsExLayered);

            Assert.Equal(once, OverlayWindowStyles.Apply(once));
        }

        [Fact]
        public void the_no_activate_style_has_the_documented_value()
        {
            Assert.Equal(0x08000000, Win32Methods.WS_EX_NOACTIVATE);
        }
    }
}
