using System;
using System.Collections.Generic;
using Carnac.Logic.Models;
using Carnac.Logic.Overlay;
using SettingsProviderNet;
using Xunit;

namespace Carnac.Tests.Overlay
{
    public class OverlayStyleControllerFacts
    {
        class FakeStyleTarget : IOverlayStyle
        {
            public readonly List<bool> Applied = new List<bool>();

            public void SetCaptureFriendly(bool captureFriendly)
            {
                Applied.Add(captureFriendly);
            }
        }

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

        public class when_the_styles_are_applied
        {
            [Fact]
            public void the_overlay_is_not_capture_friendly_by_default()
            {
                var target = new FakeStyleTarget();
                var settings = new PopupSettings();
                using (var controller = new OverlayStyleController(target, settings))
                {
                    controller.Apply();
                }

                Assert.Equal(new[] { false }, target.Applied.ToArray());
            }

            [Fact]
            public void the_setting_is_not_on_when_the_settings_file_says_nothing_about_it()
            {
                var settings = new SettingsProvider(new InMemoryStorage()).GetSettings<PopupSettings>();

                Assert.False(settings.CaptureFriendlyWindow);
            }

            [Fact]
            public void the_setting_is_read_from_the_settings_file()
            {
                var storage = new InMemoryStorage();
                storage.Values["CaptureFriendlyWindow"] = "true";

                var settings = new SettingsProvider(storage).GetSettings<PopupSettings>();

                Assert.True(settings.CaptureFriendlyWindow);
            }

            [Fact]
            public void a_capture_friendly_setting_is_applied()
            {
                var target = new FakeStyleTarget();
                var settings = new PopupSettings { CaptureFriendlyWindow = true };
                using (var controller = new OverlayStyleController(target, settings))
                {
                    controller.Apply();
                }

                Assert.Equal(new[] { true }, target.Applied.ToArray());
            }

            [Fact]
            public void nothing_is_applied_before_it_is_asked_for()
            {
                var target = new FakeStyleTarget();
                using (new OverlayStyleController(target, new PopupSettings { CaptureFriendlyWindow = true }))
                {
                }

                Assert.Equal(0, target.Applied.Count);
            }
        }

        public class when_the_setting_changes
        {
            [Fact]
            public void the_styles_follow_it_live_in_both_directions()
            {
                var target = new FakeStyleTarget();
                var settings = new PopupSettings();
                using (var controller = new OverlayStyleController(target, settings))
                {
                    controller.Apply();

                    settings.CaptureFriendlyWindow = true;
                    settings.CaptureFriendlyWindow = false;

                    Assert.Equal(new[] { false, true, false }, target.Applied.ToArray());
                }
            }

            [Fact]
            public void other_settings_leave_the_styles_alone()
            {
                var target = new FakeStyleTarget();
                var settings = new PopupSettings();
                using (var controller = new OverlayStyleController(target, settings))
                {
                    controller.Apply();

                    settings.ItemMaxWidth = 500;
                    settings.LeftOffset = 30;
                    settings.AutoUpdate = true;
                    settings.Placement = Carnac.Logic.Enums.NotificationPlacement.TopRight;

                    Assert.Equal(1, target.Applied.Count);
                }
            }

            [Fact]
            public void the_real_setting_raises_property_changed_under_the_name_that_is_listened_for()
            {
                var settings = new PopupSettings();
                var raised = new List<string>();
                settings.PropertyChanged += (sender, e) => raised.Add(e.PropertyName);

                settings.CaptureFriendlyWindow = true;

                Assert.True(raised.Contains("CaptureFriendlyWindow"));
                Assert.True(OverlayWindowStyles.AffectsStyles("CaptureFriendlyWindow"));
            }

            [Fact]
            public void a_disposed_controller_no_longer_reacts()
            {
                var target = new FakeStyleTarget();
                var settings = new PopupSettings();
                var controller = new OverlayStyleController(target, settings);
                controller.Apply();

                controller.Dispose();
                controller.Dispose();
                settings.CaptureFriendlyWindow = true;
                controller.Apply();

                Assert.Equal(1, target.Applied.Count);
            }
        }

        public class when_the_controller_is_created
        {
            [Fact]
            public void the_window_and_the_settings_are_required()
            {
                Assert.Throws<ArgumentNullException>(() => new OverlayStyleController(null, new PopupSettings()));
                Assert.Throws<ArgumentNullException>(() => new OverlayStyleController(new FakeStyleTarget(), null));
            }
        }
    }
}
