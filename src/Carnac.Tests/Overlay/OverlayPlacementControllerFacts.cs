using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using Carnac.Logic;
using Carnac.Logic.Enums;
using Carnac.Logic.Models;
using Carnac.Logic.Native;
using Carnac.Logic.Overlay;
using Microsoft.Reactive.Testing;
using Xunit;

namespace Carnac.Tests.Overlay
{
    public class OverlayPlacementControllerFacts
    {
        const string Display1 = @"\\.\DISPLAY1";
        const string Display2 = @"\\.\DISPLAY2";

        class FakeWindow : IOverlayWindow
        {
            public readonly List<PixelRect> Placed = new List<PixelRect>();
            public bool Succeeds = true;

            public double DpiScale { get; set; }

            public bool SetBounds(PixelRect bounds)
            {
                Placed.Add(bounds);
                return Succeeds;
            }

            public PixelRect Last
            {
                get { return Placed[Placed.Count - 1]; }
            }
        }

        class FakeScreenManager : IScreenManager
        {
            public List<DetailedScreen> Screens = new List<DetailedScreen>();
            public int Reads;

            public IEnumerable<DetailedScreen> GetScreens()
            {
                Reads++;
                return new List<DetailedScreen>(Screens);
            }
        }

        class FakeDisplaySettingsMonitor : IDisplaySettingsMonitor
        {
            public readonly Subject<Unit> Changes = new Subject<Unit>();

            public IObservable<Unit> DisplaySettingsChanged
            {
                get { return Changes; }
            }
        }

        class TestConcurrencyService : IConcurrencyService
        {
            public TestConcurrencyService(IScheduler scheduler)
            {
                MainThreadScheduler = scheduler;
                Default = scheduler;
            }

            public IScheduler MainThreadScheduler { get; private set; }
            public IScheduler Default { get; private set; }
        }

        static DetailedScreen Screen(int index, string deviceName, int left, int top, int width, int height, bool primary = false)
        {
            return new DetailedScreen { Index = index, DeviceName = deviceName, Left = left, Top = top, Width = width, Height = height, IsPrimary = primary };
        }

        // Two full HD monitors side by side: DISPLAY1 is the primary one, DISPLAY2 is right of it.
        class Fixture
        {
            public readonly TestScheduler Scheduler = new TestScheduler();
            public readonly FakeWindow Window = new FakeWindow { DpiScale = 1.0 };
            public readonly FakeScreenManager ScreenManager = new FakeScreenManager();
            public readonly FakeDisplaySettingsMonitor DisplayMonitor = new FakeDisplaySettingsMonitor();
            public readonly PopupSettings Settings = new PopupSettings { ItemMaxWidth = 350, Placement = NotificationPlacement.TopLeft, ScreenDeviceName = Display1 };

            public Fixture()
            {
                ScreenManager.Screens.Add(Screen(1, Display1, 0, 0, 1920, 1080, true));
                ScreenManager.Screens.Add(Screen(2, Display2, 1920, 0, 1920, 1080));
            }

            public OverlayPlacementController CreateController()
            {
                return new OverlayPlacementController(Window, ScreenManager, Settings, DisplayMonitor, new TestConcurrencyService(Scheduler));
            }

            public void Advance(double milliseconds)
            {
                Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(milliseconds).Ticks);
            }
        }

        public class when_the_window_is_placed
        {
            [Fact]
            public void it_goes_to_the_screen_the_settings_select()
            {
                var f = new Fixture();
                f.Settings.ScreenDeviceName = Display2;
                f.Settings.Placement = NotificationPlacement.TopLeft;
                using (var controller = f.CreateController())
                {
                    controller.Apply();
                }

                Assert.Equal(1, f.Window.Placed.Count);
                Assert.Equal(1921, f.Window.Last.X);
                Assert.Equal(1, f.Window.Last.Y);
                Assert.Equal(1078, f.Window.Last.Height);
            }

            [Fact]
            public void the_dpi_scale_of_the_window_decides_its_width()
            {
                var f = new Fixture();
                f.Window.DpiScale = 1.5;
                f.Settings.Placement = NotificationPlacement.TopLeft;
                var contentWidth = OverlayPlacement.GetContentWidth(350, 0, 0);
                using (var controller = f.CreateController())
                {
                    controller.Apply();
                }

                Assert.Equal((int)Math.Ceiling(contentWidth * 1.5), f.Window.Last.Width);
            }

            [Fact]
            public void nothing_is_placed_before_it_is_asked_for()
            {
                var f = new Fixture();
                using (f.CreateController())
                {
                }

                Assert.Equal(0, f.Window.Placed.Count);
                Assert.Equal(0, f.ScreenManager.Reads);
            }

            [Fact]
            public void without_any_screen_nothing_is_placed_and_nothing_fails()
            {
                var f = new Fixture();
                f.ScreenManager.Screens.Clear();
                using (var controller = f.CreateController())
                {
                    controller.Apply();
                }

                Assert.Equal(0, f.Window.Placed.Count);
            }

            [Fact]
            public void a_window_that_cannot_be_placed_is_not_a_failure()
            {
                var f = new Fixture();
                f.Window.Succeeds = false;
                using (var controller = f.CreateController())
                {
                    controller.Apply();
                    controller.Apply();
                }

                Assert.Equal(2, f.Window.Placed.Count);
            }
        }

        public class when_a_setting_changes
        {
            [Fact]
            public void the_offsets_and_the_width_place_the_window_again_without_reading_the_screens_again()
            {
                var f = new Fixture();
                f.Settings.Placement = NotificationPlacement.TopRight;
                using (var controller = f.CreateController())
                {
                    controller.Apply();
                    var before = f.Window.Last;

                    f.Settings.RightOffset = 100;
                    var afterOffset = f.Window.Last;
                    f.Settings.LeftOffset = 30;
                    f.Settings.ItemMaxWidth = 500;
                    f.Settings.Placement = NotificationPlacement.TopLeft;

                    Assert.Equal(5, f.Window.Placed.Count);
                    Assert.Equal(before.Width + 100, afterOffset.Width);
                    Assert.Equal(1, f.ScreenManager.Reads);
                    Assert.Equal(1, f.Window.Last.X);
                }
            }

            [Fact]
            public void selecting_another_screen_reads_the_screens_and_moves_the_window()
            {
                var f = new Fixture();
                using (var controller = f.CreateController())
                {
                    controller.Apply();
                    Assert.Equal(1, f.Window.Last.X);

                    f.Settings.ScreenDeviceName = Display2;
                    Assert.Equal(1921, f.Window.Last.X);
                    Assert.Equal(2, f.ScreenManager.Reads);

                    f.Settings.Screen = 2;
                    Assert.Equal(1921, f.Window.Last.X);
                    Assert.Equal(3, f.ScreenManager.Reads);
                }
            }

            [Fact]
            public void the_settings_that_do_not_change_the_window_leave_it_alone()
            {
                var f = new Fixture();
                using (var controller = f.CreateController())
                {
                    controller.Apply();

                    f.Settings.FontSize = 20;
                    f.Settings.TopOffset = 200;
                    f.Settings.BottomOffset = 200;
                    f.Settings.ItemOpacity = 0.9;
                    f.Settings.ShowApplicationIcon = true;

                    Assert.Equal(1, f.Window.Placed.Count);
                }
            }
        }

        public class when_the_displays_change
        {
            [Fact]
            public void a_burst_of_notifications_places_the_window_once_after_the_quiet_period()
            {
                var f = new Fixture();
                using (var controller = f.CreateController())
                {
                    controller.Apply();

                    f.DisplayMonitor.Changes.OnNext(Unit.Default);
                    f.Advance(200);
                    f.DisplayMonitor.Changes.OnNext(Unit.Default);
                    f.Advance(200);
                    f.DisplayMonitor.Changes.OnNext(Unit.Default);

                    // 499 ms after the last notification
                    f.Advance(499);
                    Assert.Equal(1, f.Window.Placed.Count);
                    Assert.Equal(1, f.ScreenManager.Reads);

                    f.Advance(2);
                    Assert.Equal(2, f.Window.Placed.Count);
                    Assert.Equal(2, f.ScreenManager.Reads);

                    f.Advance(5000);
                    Assert.Equal(2, f.Window.Placed.Count);
                }
            }

            [Fact]
            public void notifications_further_apart_than_the_quiet_period_are_placed_one_by_one()
            {
                var f = new Fixture();
                using (var controller = f.CreateController())
                {
                    controller.Apply();

                    f.DisplayMonitor.Changes.OnNext(Unit.Default);
                    f.Advance(1000);
                    f.DisplayMonitor.Changes.OnNext(Unit.Default);
                    f.Advance(1000);

                    Assert.Equal(3, f.Window.Placed.Count);
                }
            }

            [Fact]
            public void unplugging_the_selected_monitor_moves_the_window_to_the_primary_screen_and_plugging_it_in_moves_it_back()
            {
                var f = new Fixture();
                f.Settings.ScreenDeviceName = Display2;
                using (var controller = f.CreateController())
                {
                    controller.Apply();
                    Assert.Equal(1921, f.Window.Last.X);

                    var unplugged = f.ScreenManager.Screens[1];
                    f.ScreenManager.Screens.RemoveAt(1);
                    f.DisplayMonitor.Changes.OnNext(Unit.Default);
                    f.Advance(600);
                    Assert.Equal(1, f.Window.Last.X);
                    Assert.Equal(Display2, f.Settings.ScreenDeviceName);

                    f.ScreenManager.Screens.Add(unplugged);
                    f.DisplayMonitor.Changes.OnNext(Unit.Default);
                    f.Advance(600);
                    Assert.Equal(1921, f.Window.Last.X);
                }
            }

            [Fact]
            public void a_new_resolution_gives_the_window_the_new_height()
            {
                var f = new Fixture();
                using (var controller = f.CreateController())
                {
                    controller.Apply();
                    Assert.Equal(1078, f.Window.Last.Height);

                    f.ScreenManager.Screens[0] = Screen(1, Display1, 0, 0, 2560, 1440, true);
                    f.DisplayMonitor.Changes.OnNext(Unit.Default);
                    f.Advance(600);

                    Assert.Equal(1438, f.Window.Last.Height);
                }
            }

            [Fact]
            public void a_monitor_that_moved_in_the_arrangement_is_found_at_its_new_position()
            {
                var f = new Fixture();
                f.Settings.ScreenDeviceName = Display2;
                using (var controller = f.CreateController())
                {
                    controller.Apply();

                    f.ScreenManager.Screens[1] = Screen(2, Display2, 0, 1080, 1920, 1080);
                    f.DisplayMonitor.Changes.OnNext(Unit.Default);
                    f.Advance(600);

                    Assert.Equal(1, f.Window.Last.X);
                    Assert.Equal(1081, f.Window.Last.Y);
                }
            }
        }

        public class when_the_controller_is_disposed
        {
            [Fact]
            public void settings_and_display_changes_no_longer_place_the_window()
            {
                var f = new Fixture();
                var controller = f.CreateController();
                controller.Apply();
                f.DisplayMonitor.Changes.OnNext(Unit.Default);

                controller.Dispose();
                f.Advance(1000);
                f.Settings.LeftOffset = 50;
                f.DisplayMonitor.Changes.OnNext(Unit.Default);
                f.Advance(1000);
                controller.Apply();

                Assert.Equal(1, f.Window.Placed.Count);
            }

            [Fact]
            public void disposing_twice_is_harmless()
            {
                var f = new Fixture();
                var controller = f.CreateController();

                controller.Dispose();
                controller.Dispose();
            }
        }

        public class when_the_controller_is_created
        {
            [Fact]
            public void every_dependency_is_required()
            {
                var f = new Fixture();
                var concurrency = new TestConcurrencyService(f.Scheduler);

                Assert.Throws<ArgumentNullException>(() => new OverlayPlacementController(null, f.ScreenManager, f.Settings, f.DisplayMonitor, concurrency));
                Assert.Throws<ArgumentNullException>(() => new OverlayPlacementController(f.Window, null, f.Settings, f.DisplayMonitor, concurrency));
                Assert.Throws<ArgumentNullException>(() => new OverlayPlacementController(f.Window, f.ScreenManager, null, f.DisplayMonitor, concurrency));
                Assert.Throws<ArgumentNullException>(() => new OverlayPlacementController(f.Window, f.ScreenManager, f.Settings, null, concurrency));
                Assert.Throws<ArgumentNullException>(() => new OverlayPlacementController(f.Window, f.ScreenManager, f.Settings, f.DisplayMonitor, null));
            }

            [Fact]
            public void the_quiet_period_is_half_a_second()
            {
                Assert.Equal(TimeSpan.FromMilliseconds(500), OverlayPlacementController.DisplayChangeQuietPeriod);
            }
        }
    }
}
