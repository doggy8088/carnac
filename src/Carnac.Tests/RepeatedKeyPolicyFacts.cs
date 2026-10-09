using System.Collections.Generic;
using Carnac.Logic.Enums;
using Carnac.Logic.Models;
using SettingsProviderNet;
using Xunit;

namespace Carnac.Tests
{
    public class RepeatedKeyPolicyFacts
    {
        [Fact]
        public void the_default_policy_summarises_typed_characters_from_the_fourth_repeat()
        {
            var policy = RepeatedKeyPolicy.Default;

            Assert.Equal(RepeatedKeyGrouping.Threshold, policy.Grouping);
            Assert.Equal(4, policy.TypedCharacterThreshold);
            Assert.Equal(4, policy.GetMinimumTypedCharacterRepeat(false));
            Assert.Equal(10, policy.GetMinimumTypedCharacterRepeat(true));
            Assert.Equal(2, policy.MinimumNamedKeyRepeat);
        }

        [Fact]
        public void the_never_policy_does_not_summarise_typed_characters_but_still_summarises_named_keys()
        {
            var policy = RepeatedKeyPolicy.Never;

            Assert.Equal(RepeatedKeyGrouping.Never, policy.Grouping);
            Assert.Equal(int.MaxValue, policy.GetMinimumTypedCharacterRepeat(false));
            Assert.Equal(int.MaxValue, policy.GetMinimumTypedCharacterRepeat(true));
            Assert.Equal(2, policy.MinimumNamedKeyRepeat);
        }

        [Fact]
        public void the_threshold_is_ignored_when_grouping_is_off()
        {
            var policy = RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Never, 2);

            Assert.Equal(RepeatedKeyGrouping.Never, policy.Grouping);
            Assert.Equal(int.MaxValue, policy.GetMinimumTypedCharacterRepeat(false));
        }

        [Fact]
        public void the_supported_thresholds_are_used_as_they_are()
        {
            for (var threshold = RepeatedKeyPolicy.MinimumTypedCharacterThreshold; threshold <= RepeatedKeyPolicy.MaximumTypedCharacterThreshold; threshold++)
            {
                var policy = RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, threshold);

                Assert.Equal(RepeatedKeyGrouping.Threshold, policy.Grouping);
                Assert.Equal(threshold, policy.TypedCharacterThreshold);
                Assert.Equal(threshold, policy.GetMinimumTypedCharacterRepeat(false));
            }
        }

        [Fact]
        public void a_threshold_outside_the_supported_range_is_moved_into_it()
        {
            Assert.Equal(2, RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, 1).TypedCharacterThreshold);
            Assert.Equal(2, RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, 0).TypedCharacterThreshold);
            Assert.Equal(2, RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, -7).TypedCharacterThreshold);
            Assert.Equal(10, RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, 11).TypedCharacterThreshold);
            Assert.Equal(10, RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, int.MaxValue).TypedCharacterThreshold);
        }

        [Fact]
        public void digits_wait_for_ten_repeats_whatever_the_threshold()
        {
            Assert.Equal(10, RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, 2).GetMinimumTypedCharacterRepeat(true));
            Assert.Equal(10, RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, 10).GetMinimumTypedCharacterRepeat(true));
            Assert.Equal(2, RepeatedKeyPolicy.Create(RepeatedKeyGrouping.Threshold, 2).GetMinimumTypedCharacterRepeat(false));
        }
    }

    public class RepeatedKeyGroupingSettingsFacts
    {
        [Fact]
        public void new_settings_keep_the_previous_behaviour()
        {
            var settings = new PopupSettings();

            Assert.Equal(RepeatedKeyGrouping.Threshold, settings.RepeatedKeyGrouping);
            Assert.Equal(4, settings.RepeatedKeyThreshold);
        }

        [Fact]
        public void settings_saved_before_the_grouping_options_existed_keep_the_previous_behaviour()
        {
            var storage = new InMemorySettingsStorage();
            var settings = new SettingsProvider(storage).GetSettings<PopupSettings>();
            settings.ItemMaxWidth = 400;
            new SettingsProvider(storage).SaveSettings(settings);
            storage.RemoveEntry("RepeatedKeyGrouping");
            storage.RemoveEntry("RepeatedKeyThreshold");

            var loaded = new SettingsProvider(storage).GetSettings<PopupSettings>();

            Assert.Equal(400, loaded.ItemMaxWidth);
            Assert.Equal(RepeatedKeyGrouping.Threshold, loaded.RepeatedKeyGrouping);
            Assert.Equal(4, loaded.RepeatedKeyThreshold);
        }

        [Fact]
        public void the_grouping_choice_is_saved_and_loaded()
        {
            var storage = new InMemorySettingsStorage();
            var settings = new SettingsProvider(storage).GetSettings<PopupSettings>();
            settings.RepeatedKeyGrouping = RepeatedKeyGrouping.Never;
            settings.RepeatedKeyThreshold = 7;
            new SettingsProvider(storage).SaveSettings(settings);

            var loaded = new SettingsProvider(storage).GetSettings<PopupSettings>();

            Assert.Equal(RepeatedKeyGrouping.Never, loaded.RepeatedKeyGrouping);
            Assert.Equal(7, loaded.RepeatedKeyThreshold);
        }

        class InMemorySettingsStorage : ISettingsStorage
        {
            readonly Dictionary<string, Dictionary<string, string>> saved = new Dictionary<string, Dictionary<string, string>>();

            public void Save(string key, Dictionary<string, string> values)
            {
                saved[key] = new Dictionary<string, string>(values);
            }

            public Dictionary<string, string> Load(string key)
            {
                Dictionary<string, string> values;
                return saved.TryGetValue(key, out values)
                    ? new Dictionary<string, string>(values)
                    : new Dictionary<string, string>();
            }

            // simulates a settings file written by a version that did not know the entry
            public void RemoveEntry(string entry)
            {
                foreach (var values in saved.Values)
                {
                    Assert.True(values.Remove(entry), "no saved entry named " + entry);
                }
            }
        }
    }
}
