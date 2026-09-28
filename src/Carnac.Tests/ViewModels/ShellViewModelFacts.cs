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
            public void selected_screen_left_and_top_are_copied_to_settings()
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

                Assert.Equal(640, subject.Settings.Left);
                Assert.Equal(480, subject.Settings.Top);
                Assert.Same(screens[1], subject.SelectedScreen);
            }
        }
    }
}
