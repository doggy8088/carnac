using System;
using System.Collections.Generic;
using System.Linq;
using Carnac.Logic;
using Carnac.Logic.Enums;
using Carnac.Logic.Models;
using Carnac.Logic.Native;
using Carnac.UI;
using NSubstitute;
using SettingsProviderNet;
using Xunit;

namespace Carnac.Tests.ViewModels
{
    public class ShellViewModelFacts
    {
        public class when_creating_the_new_viewmodel : SpecificationFor<PreferencesViewModel>
        {
            readonly ISettingsProvider settingsService = Substitute.For<ISettingsProvider>();
            readonly IScreenManager screenManager = Substitute.For<IScreenManager>();

            public override PreferencesViewModel Given()
            {
                settingsService.GetSettings<PopupSettings>().Returns(new PopupSettings());
                return new PreferencesViewModel(settingsService, screenManager);
            }

            public override void When()
            {
                // do nothing
            }

            [Fact]
            public void ScreenManager_Always_Fetches_CurrentScreens()
            {
                screenManager.Received().GetScreens();
            }

            [Fact]
            public void SettingsService_Always_Fetches_ExistingSettings()
            {
                settingsService.Received().GetSettings<PopupSettings>();
            }
        }

        public class when_the_settings_file_is_defined : SpecificationFor<PreferencesViewModel>
        {
            readonly ISettingsProvider settingsService = Substitute.For<ISettingsProvider>();
            readonly IScreenManager screenManager = Substitute.For<IScreenManager>();
            readonly PopupSettings popupSettings = new PopupSettings();

            public override PreferencesViewModel Given()
            {
                settingsService.GetSettings<PopupSettings>().Returns(popupSettings);
                return new PreferencesViewModel(settingsService, screenManager);
            }

            public override void When()
            {
                // do nothing
            }

            [Fact]
            public void the_settings_file_is_the_existing_instance()
            {
                Assert.Equal(popupSettings, Subject.Settings);
            }
        }

        public class when_the_settings_file_is_not_defined : SpecificationFor<PreferencesViewModel>
        {
            readonly ISettingsProvider settingsService = Substitute.For<ISettingsProvider>();
            readonly IScreenManager screenManager = Substitute.For<IScreenManager>();
            readonly PopupSettings popupSettings = Substitute.For<PopupSettings>();

            public override PreferencesViewModel Given()
            {
                settingsService.GetSettings<PopupSettings>().Returns(popupSettings);
                return new PreferencesViewModel(settingsService, screenManager);
            }

            public override void When()
            {
                // do nothing
            }

            [Fact]
            public void the_settings_file_is_the_existing_instance()
            {
                Assert.NotNull(Subject.Settings);
            }
        }

        public class when_offering_the_repeated_key_grouping_options
        {
            [Fact]
            public void every_grouping_has_exactly_one_named_option()
            {
                var settingsService = Substitute.For<ISettingsProvider>();
                settingsService.GetSettings<PopupSettings>().Returns(new PopupSettings());

                var subject = new PreferencesViewModel(settingsService, Substitute.For<IScreenManager>());

                foreach (RepeatedKeyGrouping grouping in Enum.GetValues(typeof(RepeatedKeyGrouping)))
                {
                    var options = subject.RepeatedKeyGroupingOptions.Where(o => o.Key == grouping).ToList();
                    Assert.Equal(1, options.Count);
                    Assert.False(string.IsNullOrWhiteSpace(options[0].Value));
                }
            }
        }

        public class when_positioning_notifications_on_a_selected_screen
        {
            [Fact]
            public void the_screen_with_the_configured_index_is_selected_and_the_placement_is_checked()
            {
                var settingsService = Substitute.For<ISettingsProvider>();
                var screenManager = Substitute.For<IScreenManager>();
                var popupSettings = new PopupSettings
                {
                    Screen = 2,
                    Placement = NotificationPlacement.TopRight
                };
                var screens = new List<DetailedScreen>
                {
                    new DetailedScreen { Index = 1, Left = 0, Top = 0 },
                    new DetailedScreen { Index = 2, Left = 640, Top = 480 }
                };

                settingsService.GetSettings<PopupSettings>().Returns(popupSettings);
                screenManager.GetScreens().Returns(screens);

                var subject = new PreferencesViewModel(settingsService, screenManager);

                Assert.Same(screens[1], subject.SelectedScreen);
                Assert.True(screens[1].NotificationPlacementTopRight);
                Assert.False(screens[0].NotificationPlacementTopRight);
            }
        }

        public class when_the_screens_are_arranged_on_the_desk
        {
            static PreferencesViewModel Create(PopupSettings popupSettings, out ISettingsProvider settingsService, params DetailedScreen[] screens)
            {
                settingsService = Substitute.For<ISettingsProvider>();
                var screenManager = Substitute.For<IScreenManager>();
                settingsService.GetSettings<PopupSettings>().Returns(popupSettings);
                screenManager.GetScreens().Returns(new List<DetailedScreen>(screens));
                return new PreferencesViewModel(settingsService, screenManager);
            }

            static DetailedScreen Screen(int index, string deviceName, int left, int top, int width, int height, bool primary = false)
            {
                return new DetailedScreen { Index = index, DeviceName = deviceName, IsPrimary = primary, Left = left, Top = top, Width = width, Height = height };
            }

            [Fact]
            public void the_screens_are_drawn_in_their_real_arrangement()
            {
                ISettingsProvider settingsService;
                var below = Screen(2, @"\\.\DISPLAY2", 0, 1080, 1920, 1080);
                var subject = Create(new PopupSettings(), out settingsService, Screen(1, @"\\.\DISPLAY1", 0, 0, 1920, 1080, true), below);

                Assert.True(subject.ScreenLayoutWidth > 0);
                Assert.True(subject.ScreenLayoutHeight > 0);
                Assert.Equal(0, below.LayoutLeft);
                Assert.Equal(below.RelativeHeight, below.LayoutTop, 6);
                Assert.Equal(subject.ScreenLayoutHeight, below.LayoutTop + below.RelativeHeight, 6);
            }

            [Fact]
            public void the_configured_device_name_selects_the_screen_even_when_the_number_points_elsewhere()
            {
                ISettingsProvider settingsService;
                var second = Screen(2, @"\\.\DISPLAY2", 1920, 0, 1920, 1080);
                var subject = Create(
                    new PopupSettings { Screen = 1, ScreenDeviceName = @"\\.\DISPLAY2" },
                    out settingsService,
                    Screen(1, @"\\.\DISPLAY1", 0, 0, 1920, 1080, true),
                    second);

                Assert.Same(second, subject.SelectedScreen);
            }

            [Fact]
            public void a_screen_that_is_gone_selects_the_primary_screen()
            {
                ISettingsProvider settingsService;
                var primary = Screen(1, @"\\.\DISPLAY1", 0, 0, 1920, 1080, true);
                var subject = Create(
                    new PopupSettings { Screen = 2, ScreenDeviceName = @"\\.\DISPLAY9" },
                    out settingsService,
                    primary,
                    Screen(2, @"\\.\DISPLAY2", 1920, 0, 1920, 1080));

                Assert.Same(primary, subject.SelectedScreen);
            }

            [Fact]
            public void saving_stores_the_device_name_and_the_number_of_the_selected_screen()
            {
                ISettingsProvider settingsService;
                var popupSettings = new PopupSettings { FontColor = "White", ItemBackgroundColor = "Black" };
                var second = Screen(2, @"\\.\DISPLAY2", 1920, 0, 1920, 1080);
                var subject = Create(popupSettings, out settingsService, Screen(1, @"\\.\DISPLAY1", 0, 0, 1920, 1080, true), second);
                second.NotificationPlacementTopRight = true;
                subject.SelectedScreen = second;

                subject.SaveCommand.Execute(null);

                Assert.Equal(@"\\.\DISPLAY2", popupSettings.ScreenDeviceName);
                Assert.Equal(2, popupSettings.Screen);
                Assert.Equal(NotificationPlacement.TopRight, popupSettings.Placement);
                settingsService.Received().SaveSettings(popupSettings);
            }

            [Fact]
            public void saving_sets_the_device_name_before_the_number_so_the_overlay_never_lands_on_the_wrong_screen_in_between()
            {
                ISettingsProvider settingsService;
                var popupSettings = new PopupSettings { FontColor = "White", ItemBackgroundColor = "Black" };
                var second = Screen(2, @"\\.\DISPLAY2", 1920, 0, 1920, 1080);
                var subject = Create(popupSettings, out settingsService, Screen(1, @"\\.\DISPLAY1", 0, 0, 1920, 1080, true), second);
                subject.SelectedScreen = second;
                var raised = new List<string>();
                popupSettings.PropertyChanged += (sender, e) => raised.Add(e.PropertyName);

                subject.SaveCommand.Execute(null);

                Assert.True(raised.IndexOf("ScreenDeviceName") >= 0, "no notification for ScreenDeviceName");
                Assert.True(raised.IndexOf("Screen") > raised.IndexOf("ScreenDeviceName"));
            }
        }
    }
}
