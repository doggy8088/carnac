using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.Models;
using Carnac.Logic.MouseMonitor;
using Xunit;

namespace Carnac.Tests
{
    public class MouseClickHighlightFacts
    {
        readonly FakeInterceptMouse mouse = new FakeInterceptMouse();
        // the defaults are applied by the settings provider when settings are loaded, so a new instance has none
        readonly PopupSettings settings = new PopupSettings { ShowMouseClicks = true, ClickCircleSize = 60, ClickCircleDuration = 600 };
        readonly List<ClickHighlight> highlights = new List<ClickHighlight>();
        Func<MouseClick, OverlayLocation> toOverlayLocation = click => new OverlayLocation(click.X, click.Y);

        [Fact]
        public void nothing_is_listened_to_while_the_setting_is_off()
        {
            settings.ShowMouseClicks = false;

            using (Subscribe())
            {
                Assert.Equal(0, mouse.ActiveSubscriptions);
                Assert.Equal(0, mouse.TotalSubscriptions);

                mouse.Click(10, 20, MouseButtons.Left);
                Assert.Empty(highlights);
            }
        }

        [Fact]
        public void mouse_clicks_are_not_shown_unless_asked_for_and_the_defaults_are_sensible()
        {
            Assert.Equal(false, DefaultOf("ShowMouseClicks"));
            Assert.Equal("OrangeRed", DefaultOf("LeftClickColor"));
            Assert.Equal("Gold", DefaultOf("MiddleClickColor"));
            Assert.Equal("RoyalBlue", DefaultOf("RightClickColor"));

            var size = (int)DefaultOf("ClickCircleSize");
            var duration = (int)DefaultOf("ClickCircleDuration");
            Assert.Equal(size, ClickHighlightSettings.GetDiameter(size));
            Assert.Equal(duration, (int)ClickHighlightSettings.GetDuration(duration).TotalMilliseconds);
        }

        [Fact]
        public void a_click_is_highlighted_where_it_is_on_the_overlay()
        {
            toOverlayLocation = click => new OverlayLocation(click.X / 2.0, click.Y / 2.0);

            using (Subscribe())
            {
                mouse.Click(100, 60, MouseButtons.Left);
            }

            var highlight = Assert.Single(highlights);
            Assert.Equal(50, highlight.Location.X);
            Assert.Equal(30, highlight.Location.Y);
            Assert.Equal("OrangeRed", highlight.ColorName);
            Assert.Equal(60, highlight.Diameter);
            Assert.Equal(TimeSpan.FromMilliseconds(600), highlight.Duration);
        }

        [Fact]
        public void each_button_has_its_own_color()
        {
            settings.LeftClickColor = "Red";
            settings.MiddleClickColor = "Green";
            settings.RightClickColor = "Blue";

            using (Subscribe())
            {
                mouse.Click(1, 1, MouseButtons.Left);
                mouse.Click(1, 1, MouseButtons.Middle);
                mouse.Click(1, 1, MouseButtons.Right);
            }

            Assert.Equal(new[] { "Red", "Green", "Blue" }, highlights.ConvertAll(h => h.ColorName).ToArray());
        }

        [Fact]
        public void unknown_or_missing_colors_fall_back_to_the_defaults()
        {
            settings.LeftClickColor = "NotAColor";
            settings.MiddleClickColor = null;
            settings.RightClickColor = "royalblue";

            using (Subscribe())
            {
                mouse.Click(1, 1, MouseButtons.Left);
                mouse.Click(1, 1, MouseButtons.Middle);
                mouse.Click(1, 1, MouseButtons.Right);
            }

            Assert.Equal(new[] { "OrangeRed", "Gold", "RoyalBlue" }, highlights.ConvertAll(h => h.ColorName).ToArray());
        }

        [Fact]
        public void the_size_and_duration_of_the_ring_are_kept_within_limits()
        {
            using (Subscribe())
            {
                settings.ClickCircleSize = 5;
                settings.ClickCircleDuration = 0;
                mouse.Click(1, 1, MouseButtons.Left);

                settings.ClickCircleSize = 5000;
                settings.ClickCircleDuration = int.MaxValue;
                mouse.Click(1, 1, MouseButtons.Left);

                settings.ClickCircleSize = -3;
                settings.ClickCircleDuration = -1;
                mouse.Click(1, 1, MouseButtons.Left);

                settings.ClickCircleSize = 80;
                settings.ClickCircleDuration = 1500;
                mouse.Click(1, 1, MouseButtons.Left);
            }

            Assert.Equal(new[] { 20, 200, 20, 80 }, highlights.ConvertAll(h => h.Diameter).ToArray());
            Assert.Equal(
                new[] { 100, 3000, 100, 1500 },
                highlights.ConvertAll(h => (int)h.Duration.TotalMilliseconds).ToArray());
        }

        [Fact]
        public void settings_changes_apply_to_the_next_click()
        {
            using (Subscribe())
            {
                mouse.Click(1, 1, MouseButtons.Left);
                settings.LeftClickColor = "Purple";
                settings.ClickCircleSize = 100;
                mouse.Click(1, 1, MouseButtons.Left);
            }

            Assert.Equal("OrangeRed", highlights[0].ColorName);
            Assert.Equal("Purple", highlights[1].ColorName);
            Assert.Equal(100, highlights[1].Diameter);
        }

        [Fact]
        public void a_click_that_is_not_on_the_overlay_is_not_highlighted()
        {
            toOverlayLocation = click => click.X < 0 ? null : new OverlayLocation(click.X, click.Y);

            using (Subscribe())
            {
                mouse.Click(-50, 10, MouseButtons.Left);
                mouse.Click(50, 10, MouseButtons.Left);
            }

            var highlight = Assert.Single(highlights);
            Assert.Equal(50, highlight.Location.X);
        }

        [Fact]
        public void other_mouse_buttons_are_not_highlighted()
        {
            using (Subscribe())
            {
                mouse.Click(1, 1, MouseButtons.XButton1);
                mouse.Click(1, 1, MouseButtons.XButton2);
                mouse.Click(1, 1, MouseButtons.None);
            }

            Assert.Empty(highlights);
        }

        [Fact]
        public void the_mouse_is_only_listened_to_while_the_setting_is_on_and_again_when_it_is_switched_back_on()
        {
            settings.ShowMouseClicks = false;

            using (Subscribe())
            {
                Assert.Equal(0, mouse.ActiveSubscriptions);

                settings.ShowMouseClicks = true;
                Assert.Equal(1, mouse.ActiveSubscriptions);
                mouse.Click(1, 1, MouseButtons.Left);

                settings.ShowMouseClicks = false;
                Assert.Equal(0, mouse.ActiveSubscriptions);
                mouse.Click(2, 2, MouseButtons.Left);

                settings.ShowMouseClicks = true;
                Assert.Equal(1, mouse.ActiveSubscriptions);
                Assert.Equal(2, mouse.TotalSubscriptions);
                mouse.Click(3, 3, MouseButtons.Left);
            }

            Assert.Equal(2, highlights.Count);
            Assert.Equal(1, highlights[0].Location.X);
            Assert.Equal(3, highlights[1].Location.X);
        }

        [Fact]
        public void other_settings_changes_do_not_resubscribe_to_the_mouse()
        {
            using (Subscribe())
            {
                settings.LeftClickColor = "Red";
                settings.ClickCircleSize = 90;
                settings.ShowMouseClicks = true;
            }

            Assert.Equal(1, mouse.TotalSubscriptions);
        }

        [Fact]
        public void disposing_the_subscription_stops_listening_to_the_mouse_and_to_the_setting()
        {
            var subscription = Subscribe();
            Assert.Equal(1, mouse.ActiveSubscriptions);

            subscription.Dispose();
            Assert.Equal(0, mouse.ActiveSubscriptions);

            settings.ShowMouseClicks = false;
            settings.ShowMouseClicks = true;
            Assert.Equal(0, mouse.ActiveSubscriptions);
            Assert.Equal(1, mouse.TotalSubscriptions);
        }

        [Fact]
        public void a_mouse_that_cannot_be_listened_to_is_survived()
        {
            mouse.FailToSubscribe = true;
            var errors = new List<Exception>();

            using (Subscribe(errors.Add))
            {
                Assert.Empty(errors);
                Assert.Empty(highlights);
            }
        }

        [Fact]
        public void the_highlighter_requires_what_it_works_with()
        {
            Assert.Throws<ArgumentNullException>(() => new MouseClickHighlighter(null, settings, toOverlayLocation));
            Assert.Throws<ArgumentNullException>(() => new MouseClickHighlighter(mouse, null, toOverlayLocation));
            Assert.Throws<ArgumentNullException>(() => new MouseClickHighlighter(mouse, settings, null));
        }

        static object DefaultOf(string setting)
        {
            var attribute = (DefaultValueAttribute)Attribute.GetCustomAttribute(typeof(PopupSettings).GetProperty(setting), typeof(DefaultValueAttribute));
            return attribute.Value;
        }

        IDisposable Subscribe(Action<Exception> onError = null)
        {
            var highlighter = new MouseClickHighlighter(mouse, settings, click => toOverlayLocation(click));
            return highlighter.GetHighlightStream().Subscribe(highlights.Add, onError ?? (e => { throw e; }));
        }

        class FakeInterceptMouse : IInterceptMouse
        {
            readonly Subject<MouseClick> clicks = new Subject<MouseClick>();

            public int ActiveSubscriptions { get; private set; }

            public int TotalSubscriptions { get; private set; }

            public bool FailToSubscribe { get; set; }

            public void Click(int x, int y, MouseButtons button)
            {
                clicks.OnNext(new MouseClick(x, y, button));
            }

            public IObservable<MouseClick> GetClickStream()
            {
                return Observable.Create<MouseClick>(observer =>
                {
                    if (FailToSubscribe)
                        throw new InvalidOperationException("no hook");

                    TotalSubscriptions++;
                    ActiveSubscriptions++;
                    var subscription = clicks.Subscribe(observer);
                    return Disposable.Create(() =>
                    {
                        ActiveSubscriptions--;
                        subscription.Dispose();
                    });
                });
            }
        }
    }

    public class InterceptMouseFacts
    {
        const int WM_MOUSEMOVE = 0x0200;
        const int WM_LBUTTONDOWN = 0x0201;
        const int WM_LBUTTONUP = 0x0202;
        const int WM_RBUTTONDOWN = 0x0204;
        const int WM_MBUTTONDOWN = 0x0207;
        const int WM_MOUSEWHEEL = 0x020A;
        const int WM_XBUTTONDOWN = 0x020B;

        [Fact]
        public void the_left_middle_and_right_button_presses_are_clicks_at_the_position_of_the_hook_data()
        {
            Assert.Equal(MouseButtons.Left, CreateClick(WM_LBUTTONDOWN, 12, 34).Button);
            Assert.Equal(MouseButtons.Middle, CreateClick(WM_MBUTTONDOWN, 12, 34).Button);
            Assert.Equal(MouseButtons.Right, CreateClick(WM_RBUTTONDOWN, 12, 34).Button);

            var click = CreateClick(WM_LBUTTONDOWN, -1920, 1080);
            Assert.Equal(-1920, click.X);
            Assert.Equal(1080, click.Y);
        }

        [Fact]
        public void everything_else_is_not_a_click_and_its_data_is_not_read()
        {
            foreach (var message in new[] { WM_MOUSEMOVE, WM_LBUTTONUP, WM_MOUSEWHEEL, WM_XBUTTONDOWN })
            {
                Assert.Null(InterceptMouse.CreateClick(new IntPtr(message), IntPtr.Zero));
            }
        }

        static MouseClick CreateClick(int message, int x, int y)
        {
            // the position is the start of the MSLLHOOKSTRUCT the hook is given
            var hookData = Marshal.AllocHGlobal(32);
            try
            {
                Marshal.WriteInt32(hookData, 0, x);
                Marshal.WriteInt32(hookData, 4, y);
                return InterceptMouse.CreateClick(new IntPtr(message), hookData);
            }
            finally
            {
                Marshal.FreeHGlobal(hookData);
            }
        }
    }
}
