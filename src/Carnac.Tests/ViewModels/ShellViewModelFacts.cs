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
                return new PreferencesViewModel(settingsService, screenManager, Substitute.For<IPreviewService>());
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
                return new PreferencesViewModel(settingsService, screenManager, Substitute.For<IPreviewService>());
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
                return new PreferencesViewModel(settingsService, screenManager, Substitute.For<IPreviewService>());
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

                var subject = new PreferencesViewModel(settingsService, screenManager, Substitute.For<IPreviewService>());

                Assert.Same(screens[1], subject.SelectedScreen);
                Assert.True(screens[1].NotificationPlacementTopRight);
                Assert.False(screens[0].NotificationPlacementTopRight);
            }
        }

        public class when_previewing_the_settings
        {
            readonly IPreviewService previewService = Substitute.For<IPreviewService>();
            readonly IDisposable visit = Substitute.For<IDisposable>();
            readonly PopupSettings popupSettings = new PopupSettings { FontColor = "White", ItemBackgroundColor = "Black" };

            PreferencesViewModel Create()
            {
                var settingsService = Substitute.For<ISettingsProvider>();
                var screenManager = Substitute.For<IScreenManager>();
                settingsService.GetSettings<PopupSettings>().Returns(popupSettings);
                previewService.Show().Returns(visit);
                return new PreferencesViewModel(settingsService, screenManager, previewService);
            }

            [Fact]
            public void creating_the_view_model_does_not_show_the_preview_by_itself()
            {
                Create();

                previewService.DidNotReceive().Show();
            }

            [Fact]
            public void starting_the_preview_shows_it()
            {
                var subject = Create();

                subject.StartPreview();

                previewService.Received(1).Show();
            }

            [Fact]
            public void starting_the_preview_twice_shows_it_once()
            {
                var subject = Create();

                subject.StartPreview();
                subject.StartPreview();

                previewService.Received(1).Show();
            }

            [Fact]
            public void stopping_the_preview_hides_it_once()
            {
                var subject = Create();
                subject.StartPreview();

                subject.StopPreview();
                subject.StopPreview();

                visit.Received(1).Dispose();
            }

            [Fact]
            public void stopping_a_preview_that_was_never_started_does_nothing()
            {
                var subject = Create();

                subject.StopPreview();

                visit.DidNotReceive().Dispose();
            }

            [Fact]
            public void the_preview_can_be_started_again_after_it_was_stopped()
            {
                var subject = Create();
                subject.StartPreview();
                subject.StopPreview();

                subject.StartPreview();

                previewService.Received(2).Show();
            }

            [Fact]
            public void the_font_color_shows_on_the_overlay_as_soon_as_it_is_picked()
            {
                var subject = Create();

                subject.FontColor = subject.AvailableColors.First(c => c.Name == "Red");

                Assert.Equal("Red", popupSettings.FontColor);
            }

            [Fact]
            public void the_background_color_shows_on_the_overlay_as_soon_as_it_is_picked()
            {
                var subject = Create();

                subject.ItemBackgroundColor = subject.AvailableColors.First(c => c.Name == "Blue");

                Assert.Equal("Blue", popupSettings.ItemBackgroundColor);
            }

            [Fact]
            public void the_colors_that_are_in_the_settings_are_selected_without_changing_them()
            {
                var subject = Create();

                Assert.Equal("White", subject.FontColor.Name);
                Assert.Equal("Black", subject.ItemBackgroundColor.Name);
                Assert.Equal("White", popupSettings.FontColor);
                Assert.Equal("Black", popupSettings.ItemBackgroundColor);
            }

            [Fact]
            public void clearing_a_color_does_not_clear_the_setting()
            {
                var subject = Create();

                subject.FontColor = null;

                Assert.Equal("White", popupSettings.FontColor);
            }

            [Fact]
            public void selecting_a_screen_and_placement_shows_on_the_overlay_immediately()
            {
                var settingsService = Substitute.For<ISettingsProvider>();
                var screenManager = Substitute.For<IScreenManager>();
                var first = new DetailedScreen { Index = 1, DeviceName = @"\\.\DISPLAY1", IsPrimary = true };
                var second = new DetailedScreen { Index = 2, DeviceName = @"\\.\DISPLAY2" };
                settingsService.GetSettings<PopupSettings>().Returns(popupSettings);
                screenManager.GetScreens().Returns(new List<DetailedScreen> { first, second });
                var subject = new PreferencesViewModel(settingsService, screenManager, previewService);

                subject.SelectScreenAndPlacement(second, NotificationPlacement.TopRight);

                Assert.Same(second, subject.SelectedScreen);
                Assert.Equal(@"\\.\DISPLAY2", popupSettings.ScreenDeviceName);
                Assert.Equal(2, popupSettings.Screen);
                Assert.Equal(NotificationPlacement.TopRight, popupSettings.Placement);
                Assert.True(second.NotificationPlacementTopRight);
                Assert.False(first.NotificationPlacementBottomLeft);
            }

            [Fact]
            public void the_preview_service_is_required()
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new PreferencesViewModel(Substitute.For<ISettingsProvider>(), Substitute.For<IScreenManager>(), null));
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
                return new PreferencesViewModel(settingsService, screenManager, Substitute.For<IPreviewService>());
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

            [Fact]
            public void applying_saves_the_settings_without_closing_the_window()
            {
                ISettingsProvider settingsService;
                var popupSettings = new PopupSettings { FontColor = "White", ItemBackgroundColor = "Black" };
                var subject = Create(popupSettings, out settingsService, Screen(1, @"\\.\DISPLAY1", 0, 0, 1920, 1080, true));
                var closed = false;
                subject.CloseRequested += (sender, e) => closed = true;

                subject.ApplyCommand.Execute(null);

                settingsService.Received(1).SaveSettings(popupSettings);
                Assert.False(closed);
            }

            [Fact]
            public void saving_saves_the_settings_and_requests_closing_the_window()
            {
                ISettingsProvider settingsService;
                var popupSettings = new PopupSettings { FontColor = "White", ItemBackgroundColor = "Black" };
                var subject = Create(popupSettings, out settingsService, Screen(1, @"\\.\DISPLAY1", 0, 0, 1920, 1080, true));
                var closed = false;
                subject.CloseRequested += (sender, e) => closed = true;

                subject.SaveCommand.Execute(null);

                settingsService.Received(1).SaveSettings(popupSettings);
                Assert.True(closed);
            }

            [Fact]
            public void canceling_reverts_unapplied_changes_and_requests_closing_the_window()
            {
                ISettingsProvider settingsService;
                var first = Screen(1, @"\\.\DISPLAY1", 0, 0, 1920, 1080, true);
                var second = Screen(2, @"\\.\DISPLAY2", 1920, 0, 1920, 1080);
                var popupSettings = new PopupSettings
                {
                    FontColor = "White",
                    ItemBackgroundColor = "Black",
                    FontSize = 40,
                    TopOffset = 10,
                    Screen = 1,
                    ScreenDeviceName = @"\\.\DISPLAY1",
                    Placement = NotificationPlacement.BottomLeft,
                    SilentModeHotkey = "Ctrl+Alt+P",
                    PauseHotkey = string.Empty
                };
                var subject = Create(popupSettings, out settingsService, first, second);
                var closed = false;
                subject.CloseRequested += (sender, e) => closed = true;

                subject.FontColor = subject.AvailableColors.First(c => c.Name == "Red");
                subject.ItemBackgroundColor = subject.AvailableColors.First(c => c.Name == "Blue");
                subject.Settings.FontSize = 24;
                subject.Settings.TopOffset = 99;
                subject.SelectScreenAndPlacement(second, NotificationPlacement.TopRight);
                subject.CapturePauseHotkey(System.Windows.Forms.Keys.O, true, true, false, false);

                subject.CancelCommand.Execute(null);

                Assert.True(closed);
                settingsService.DidNotReceive().SaveSettings(Arg.Any<PopupSettings>());
                Assert.Equal("White", popupSettings.FontColor);
                Assert.Equal("Black", popupSettings.ItemBackgroundColor);
                Assert.Equal(40, popupSettings.FontSize);
                Assert.Equal(10, popupSettings.TopOffset);
                Assert.Equal(1, popupSettings.Screen);
                Assert.Equal(@"\\.\DISPLAY1", popupSettings.ScreenDeviceName);
                Assert.Equal(NotificationPlacement.BottomLeft, popupSettings.Placement);
                Assert.Equal("White", subject.FontColor.Name);
                Assert.Equal("Black", subject.ItemBackgroundColor.Name);
                Assert.Same(first, subject.SelectedScreen);
                Assert.True(first.NotificationPlacementBottomLeft);
                Assert.False(second.NotificationPlacementTopRight);
                Assert.Equal(string.Empty, subject.PauseHotkey);
            }

            [Fact]
            public void reverting_after_apply_restores_the_applied_settings_instead_of_the_initial_ones()
            {
                ISettingsProvider settingsService;
                var popupSettings = new PopupSettings
                {
                    FontColor = "White",
                    ItemBackgroundColor = "Black",
                    FontSize = 40
                };
                var subject = Create(popupSettings, out settingsService, Screen(1, @"\\.\DISPLAY1", 0, 0, 1920, 1080, true));

                subject.Settings.FontSize = 32;
                subject.FontColor = subject.AvailableColors.First(c => c.Name == "Green");
                subject.ApplyCommand.Execute(null);

                subject.Settings.FontSize = 18;
                subject.FontColor = subject.AvailableColors.First(c => c.Name == "Red");
                subject.RevertUnappliedChanges();

                Assert.Equal(32, popupSettings.FontSize);
                Assert.Equal("Green", popupSettings.FontColor);
                Assert.Equal("Green", subject.FontColor.Name);
            }

            [Fact]
            public void reverting_after_save_keeps_the_saved_settings()
            {
                ISettingsProvider settingsService;
                var popupSettings = new PopupSettings
                {
                    FontColor = "White",
                    ItemBackgroundColor = "Black",
                    FontSize = 40
                };
                var subject = Create(popupSettings, out settingsService, Screen(1, @"\\.\DISPLAY1", 0, 0, 1920, 1080, true));

                subject.Settings.FontSize = 26;
                subject.SaveCommand.Execute(null);
                subject.RevertUnappliedChanges();

                Assert.Equal(26, popupSettings.FontSize);
            }

            [Fact]
            public void canceling_after_reset_to_defaults_restores_the_previously_saved_settings()
            {
                var storage = new InMemorySettingsStorage();
                var provider = new SettingsProvider(storage);
                var settings = provider.GetSettings<PopupSettings>();
                settings.FontSize = 28;
                settings.FontColor = "Yellow";
                settings.ItemBackgroundColor = "Navy";
                provider.SaveSettings(settings);

                var screenManager = Substitute.For<IScreenManager>();
                screenManager.GetScreens().Returns(new List<DetailedScreen> { Screen(1, @"\\.\DISPLAY1", 0, 0, 1920, 1080, true) });
                var subject = new PreferencesViewModel(provider, screenManager, Substitute.For<IPreviewService>());

                subject.ResetToDefaultsCommand.Execute(null);
                Assert.Equal(40, settings.FontSize);

                subject.CancelCommand.Execute(null);

                Assert.Equal(28, settings.FontSize);
                Assert.Equal("Yellow", settings.FontColor);
                Assert.Equal("Navy", settings.ItemBackgroundColor);
                Assert.Equal(28, new SettingsProvider(storage).GetSettings<PopupSettings>().FontSize);
            }
        }
    }
}
