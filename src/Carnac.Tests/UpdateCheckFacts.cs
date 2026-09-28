using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.Models;
using NSubstitute;
using SettingsProviderNet;
using Xunit;

namespace Carnac.Tests
{
    public class LatestReleaseFacts
    {
        // shortened, but shaped like the answer of https://api.github.com/repos/doggy8088/carnac/releases/latest
        const string GitHubAnswer = @"{
  ""url"": ""https://api.github.com/repos/doggy8088/carnac/releases/1"",
  ""html_url"": ""https://github.com/doggy8088/carnac/releases/tag/v2.4.1"",
  ""id"": 1,
  ""author"": { ""login"": ""doggy8088"", ""id"": 2 },
  ""tag_name"": ""v2.4.1"",
  ""name"": ""Carnac 2.4.1"",
  ""draft"": false,
  ""prerelease"": false,
  ""assets"": [ { ""name"": ""Carnac-2.4.1-Setup.exe"", ""size"": 12345 } ],
  ""body"": ""Line 1\nLine \""two\""""
}";

        [Fact]
        public void reads_the_fields_carnac_needs_and_ignores_the_rest()
        {
            var release = LatestRelease.Parse(GitHubAnswer);

            Assert.Equal("v2.4.1", release.TagName);
            Assert.Equal("https://github.com/doggy8088/carnac/releases/tag/v2.4.1", release.HtmlUrl);
            Assert.False(release.IsDraft);
            Assert.False(release.IsPreRelease);
        }

        [Fact]
        public void reads_the_draft_and_pre_release_flags()
        {
            var release = LatestRelease.Parse(@"{""tag_name"":""v3.0.0-rc.1"",""draft"":true,""prerelease"":true}");

            Assert.True(release.IsDraft);
            Assert.True(release.IsPreRelease);
            Assert.Null(release.HtmlUrl);
        }

        [Fact]
        public void rejects_text_that_is_not_json()
        {
            Assert.Throws<SerializationException>(() => LatestRelease.Parse("<html>rate limit exceeded</html>"));
        }

        [Fact]
        public void rejects_an_answer_without_a_tag()
        {
            Assert.Throws<SerializationException>(() => LatestRelease.Parse(@"{""message"":""Not Found""}"));
            Assert.Throws<SerializationException>(() => LatestRelease.Parse(@"{""tag_name"":""  ""}"));
        }

        [Fact]
        public void rejects_empty_text()
        {
            Assert.Throws<SerializationException>(() => LatestRelease.Parse(""));
            Assert.Throws<SerializationException>(() => LatestRelease.Parse(null));
        }
    }

    public class UpdateNoticeFacts
    {
        static readonly ReleaseVersion Current = ReleaseVersion.FromVersion(new Version(2, 3, 0, 0));

        static ReleaseVersion Version(string text)
        {
            ReleaseVersion version;
            Assert.True(ReleaseVersion.TryParse(text, out version));
            return version;
        }

        static UpdateNotice Notice(string tag, string htmlUrl)
        {
            return UpdateNotice.Create(Current, Version(tag), new LatestRelease { TagName = tag, HtmlUrl = htmlUrl });
        }

        [Fact]
        public void names_the_new_version_in_the_title()
        {
            var notice = Notice("v2.4.1", "https://github.com/doggy8088/carnac/releases/tag/v2.4.1");

            Assert.Equal("Carnac 2.4.1 is available", notice.Title);
        }

        [Fact]
        public void the_text_names_the_current_version_the_release_page_and_a_winget_example()
        {
            var notice = Notice("v2.4.1", "https://github.com/doggy8088/carnac/releases/tag/v2.4.1");

            Assert.Contains("2.3.0", notice.Text);
            Assert.Contains("https://github.com/doggy8088/carnac/releases/tag/v2.4.1", notice.Text);
            Assert.Contains("winget upgrade doggy8088.Carnac", notice.Text);
            Assert.Contains("package manager", notice.Text);
        }

        [Fact]
        public void a_click_opens_the_release_page()
        {
            var notice = Notice("v2.4.1", "https://github.com/doggy8088/carnac/releases/tag/v2.4.1");

            Assert.Equal("https://github.com/doggy8088/carnac/releases/tag/v2.4.1", notice.Url);
        }

        [Fact]
        public void stays_within_the_limits_of_a_tray_balloon()
        {
            var notice = Notice("v2.4.1-beta.1234567890123456789012345678901234567890123456789012345678901234567890",
                "https://github.com/doggy8088/carnac/releases/tag/" + new string('x', 200));

            Assert.True(notice.Title.Length <= 63);
            Assert.True(notice.Text.Length <= 255);
        }

        [Fact]
        public void an_address_of_another_site_is_replaced_by_the_releases_page()
        {
            Assert.Equal(UpdateNotice.ReleasesPageUrl, Notice("v2.4.1", "https://evil.example.com/doggy8088/carnac/releases/tag/v2.4.1").Url);
            Assert.Equal(UpdateNotice.ReleasesPageUrl, Notice("v2.4.1", "https://github.com.evil.example.com/doggy8088/carnac/").Url);
        }

        [Fact]
        public void an_address_of_another_repository_or_scheme_is_replaced_by_the_releases_page()
        {
            Assert.Equal(UpdateNotice.ReleasesPageUrl, Notice("v2.4.1", "https://github.com/someone-else/carnac/releases/tag/v2.4.1").Url);
            Assert.Equal(UpdateNotice.ReleasesPageUrl, Notice("v2.4.1", "http://github.com/doggy8088/carnac/releases/tag/v2.4.1").Url);
            Assert.Equal(UpdateNotice.ReleasesPageUrl, Notice("v2.4.1", "file:///C:/Windows/System32/calc.exe").Url);
            Assert.Equal(UpdateNotice.ReleasesPageUrl, Notice("v2.4.1", "calc.exe").Url);
        }

        [Fact]
        public void a_missing_address_is_replaced_by_the_releases_page()
        {
            Assert.Equal(UpdateNotice.ReleasesPageUrl, Notice("v2.4.1", null).Url);
            Assert.Equal(UpdateNotice.ReleasesPageUrl, Notice("v2.4.1", "").Url);
        }

        [Fact]
        public void create_validates_its_arguments()
        {
            var release = new LatestRelease { TagName = "v2.4.1" };
            Assert.Throws<ArgumentNullException>(() => UpdateNotice.Create(null, Version("2.4.1"), release));
            Assert.Throws<ArgumentNullException>(() => UpdateNotice.Create(Current, null, release));
            Assert.Throws<ArgumentNullException>(() => UpdateNotice.Create(Current, Version("2.4.1"), null));
        }
    }

    public class UpdateCheckerFacts
    {
        static readonly ReleaseVersion Current = ReleaseVersion.FromVersion(new Version(2, 3, 0, 0));

        readonly FakeFeed feed = new FakeFeed();
        readonly FakeHistory history = new FakeHistory();
        readonly ILogger logger = Substitute.For<ILogger>();
        DateTime now = new DateTime(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc);

        UpdateChecker CreateChecker()
        {
            return new UpdateChecker(feed, history, Current, logger, () => now);
        }

        static string ReleaseJson(string tag, bool draft = false, bool prerelease = false)
        {
            return string.Format(@"{{""tag_name"":""{0}"",""html_url"":""https://github.com/doggy8088/carnac/releases/tag/{0}"",""draft"":{1},""prerelease"":{2}}}",
                tag, draft ? "true" : "false", prerelease ? "true" : "false");
        }

        [Fact]
        public void reports_a_newer_release()
        {
            feed.Json = ReleaseJson("v2.4.1");

            var result = CreateChecker().CheckForUpdate();

            Assert.Equal(UpdateCheckOutcome.UpdateAvailable, result.Outcome);
            Assert.Equal("2.4.1", result.LatestVersion.ToString());
            Assert.Equal("v2.4.1", result.Release.TagName);
        }

        [Fact]
        public void reports_up_to_date_for_the_same_or_an_older_release()
        {
            feed.Json = ReleaseJson("v2.3.0");
            Assert.Equal(UpdateCheckOutcome.UpToDate, CreateChecker().CheckForUpdate().Outcome);

            history.Last = null;
            feed.Json = ReleaseJson("v2.2.0");
            Assert.Equal(UpdateCheckOutcome.UpToDate, CreateChecker().CheckForUpdate().Outcome);
        }

        [Fact]
        public void a_pre_release_of_the_current_version_is_not_an_update()
        {
            feed.Json = ReleaseJson("v2.3.0-rc.1");

            Assert.Equal(UpdateCheckOutcome.UpToDate, CreateChecker().CheckForUpdate().Outcome);
        }

        [Fact]
        public void drafts_and_github_pre_releases_are_never_offered()
        {
            feed.Json = ReleaseJson("v9.0.0", draft: true);
            Assert.Equal(UpdateCheckOutcome.UpToDate, CreateChecker().CheckForUpdate().Outcome);

            history.Last = null;
            feed.Json = ReleaseJson("v9.0.0", prerelease: true);
            Assert.Equal(UpdateCheckOutcome.UpToDate, CreateChecker().CheckForUpdate().Outcome);
        }

        [Fact]
        public void a_malformed_tag_is_a_failure_and_is_logged()
        {
            feed.Json = ReleaseJson("nightly-build");

            var result = CreateChecker().CheckForUpdate();

            Assert.Equal(UpdateCheckOutcome.Failed, result.Outcome);
            logger.Received().Log(LogLevel.Warning, Arg.Is<string>(text => text.Contains("nightly-build")), null);
        }

        [Fact]
        public void checks_at_most_once_a_day()
        {
            feed.Json = ReleaseJson("v2.4.1");
            var checker = CreateChecker();
            Assert.Equal(UpdateCheckOutcome.UpdateAvailable, checker.CheckForUpdate().Outcome);
            Assert.Equal(1, feed.Requests);

            now = now.AddHours(23);
            var second = checker.CheckForUpdate();
            now = now.AddMinutes(30);
            var third = checker.CheckForUpdate();

            Assert.Equal(UpdateCheckOutcome.NotDue, second.Outcome);
            Assert.Equal(UpdateCheckOutcome.NotDue, third.Outcome);
            Assert.Equal(1, feed.Requests);
        }

        [Fact]
        public void checks_again_after_a_day()
        {
            feed.Json = ReleaseJson("v2.4.1");
            var checker = CreateChecker();
            checker.CheckForUpdate();

            now = now.AddDays(1);
            var result = checker.CheckForUpdate();

            Assert.Equal(UpdateCheckOutcome.UpdateAvailable, result.Outcome);
            Assert.Equal(2, feed.Requests);
        }

        [Fact]
        public void a_check_in_an_earlier_run_counts()
        {
            history.Last = now.AddHours(-3);

            var result = CreateChecker().CheckForUpdate();

            Assert.Equal(UpdateCheckOutcome.NotDue, result.Outcome);
            Assert.Equal(0, feed.Requests);
        }

        [Fact]
        public void a_last_check_in_the_future_means_the_clock_was_set_back_and_is_ignored()
        {
            history.Last = now.AddDays(5);
            feed.Json = ReleaseJson("v2.4.1");

            Assert.Equal(UpdateCheckOutcome.UpdateAvailable, CreateChecker().CheckForUpdate().Outcome);
        }

        [Fact]
        public void records_the_time_of_the_check()
        {
            feed.Json = ReleaseJson("v2.3.0");

            CreateChecker().CheckForUpdate();

            Assert.Equal(now, history.Last);
        }

        [Fact]
        public void a_failing_lookup_is_logged_not_thrown_and_not_repeated_within_the_day()
        {
            feed.Failure = new InvalidOperationException("offline");
            var checker = CreateChecker();

            var result = checker.CheckForUpdate();
            now = now.AddHours(1);
            var again = checker.CheckForUpdate();

            Assert.Equal(UpdateCheckOutcome.Failed, result.Outcome);
            Assert.Equal(UpdateCheckOutcome.NotDue, again.Outcome);
            Assert.Equal(1, feed.Requests);
            logger.Received(1).Log(LogLevel.Warning, Arg.Any<string>(), Arg.Is<Exception>(ex => ex.Message == "offline"));
            logger.DidNotReceive().Log(LogLevel.Error, Arg.Any<string>(), Arg.Any<Exception>());
        }

        [Fact]
        public void an_unreadable_answer_is_a_logged_failure()
        {
            feed.Json = "<html>Service Unavailable</html>";

            var result = CreateChecker().CheckForUpdate();

            Assert.Equal(UpdateCheckOutcome.Failed, result.Outcome);
            logger.Received(1).Log(LogLevel.Warning, Arg.Any<string>(), Arg.Any<SerializationException>());
        }

        [Fact]
        public void settings_that_cannot_be_read_or_saved_are_a_logged_failure_and_no_request_is_made()
        {
            history.Failure = new InvalidOperationException("settings are broken");

            var result = CreateChecker().CheckForUpdate();

            Assert.Equal(UpdateCheckOutcome.Failed, result.Outcome);
            Assert.Equal(0, feed.Requests);
        }

        [Fact]
        public void constructor_validates_its_arguments()
        {
            Assert.Throws<ArgumentNullException>(() => new UpdateChecker(null, history, Current, logger, () => now));
            Assert.Throws<ArgumentNullException>(() => new UpdateChecker(feed, null, Current, logger, () => now));
            Assert.Throws<ArgumentNullException>(() => new UpdateChecker(feed, history, null, logger, () => now));
            Assert.Throws<ArgumentNullException>(() => new UpdateChecker(feed, history, Current, null, () => now));
            Assert.Throws<ArgumentNullException>(() => new UpdateChecker(feed, history, Current, logger, null));
        }

        class FakeFeed : IReleaseFeed
        {
            public string Json;
            public Exception Failure;
            public int Requests;

            public string GetLatestReleaseJson()
            {
                Requests++;
                if (Failure != null)
                    throw Failure;
                return Json;
            }
        }

        class FakeHistory : IUpdateCheckHistory
        {
            public DateTime? Last;
            public Exception Failure;

            public DateTime? LastCheckUtc
            {
                get
                {
                    if (Failure != null)
                        throw Failure;
                    return Last;
                }
            }

            public void RecordCheck(DateTime utcNow)
            {
                Last = utcNow;
            }
        }
    }

    public class UpdateCheckRunnerFacts
    {
        static readonly ReleaseVersion Current = ReleaseVersion.FromVersion(new Version(2, 3, 0, 0));

        readonly IReleaseFeed feed = Substitute.For<IReleaseFeed>();
        readonly ITrayNotifier notifier = Substitute.For<ITrayNotifier>();
        readonly ILogger logger = Substitute.For<ILogger>();
        readonly List<string> opened = new List<string>();
        readonly InMemoryStorage storage = new InMemoryStorage();
        DateTime now = new DateTime(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc);

        UpdateCheckRunner CreateRunner()
        {
            var history = new SettingsUpdateCheckHistory(new SettingsProvider(storage));
            var checker = new UpdateChecker(feed, history, Current, logger, () => now);
            return new UpdateCheckRunner(checker, Current, notifier, opened.Add, logger);
        }

        [Fact]
        public void shows_one_balloon_for_a_newer_release_and_a_click_opens_the_release_page()
        {
            feed.GetLatestReleaseJson().Returns(@"{""tag_name"":""v2.4.1"",""html_url"":""https://github.com/doggy8088/carnac/releases/tag/v2.4.1""}");
            Action onClick = null;
            notifier.When(n => n.ShowBalloon(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ToolTipIcon>(), Arg.Any<Action>()))
                .Do(call => onClick = call.Arg<Action>());

            CreateRunner().Run();

            notifier.Received(1).ShowBalloon("Carnac 2.4.1 is available", Arg.Is<string>(text => text.Contains("releases/tag/v2.4.1")), ToolTipIcon.Info, Arg.Any<Action>());
            Assert.Empty(opened);
            onClick();
            Assert.Equal(new[] { "https://github.com/doggy8088/carnac/releases/tag/v2.4.1" }, opened.ToArray());
        }

        [Fact]
        public void shows_nothing_when_carnac_is_up_to_date()
        {
            feed.GetLatestReleaseJson().Returns(@"{""tag_name"":""v2.3.0""}");

            CreateRunner().Run();

            notifier.DidNotReceiveWithAnyArgs().ShowBalloon(null, null, ToolTipIcon.None, null);
        }

        [Fact]
        public void shows_nothing_and_does_not_throw_when_the_lookup_fails()
        {
            feed.GetLatestReleaseJson().Returns(call => { throw new System.Net.WebException("offline"); });

            CreateRunner().Run();

            notifier.DidNotReceiveWithAnyArgs().ShowBalloon(null, null, ToolTipIcon.None, null);
            logger.Received(1).Log(LogLevel.Warning, Arg.Any<string>(), Arg.Any<System.Net.WebException>());
        }

        [Fact]
        public void does_not_notify_twice_on_the_same_day()
        {
            feed.GetLatestReleaseJson().Returns(@"{""tag_name"":""v2.4.1""}");
            var runner = CreateRunner();

            runner.Run();
            now = now.AddHours(2);
            CreateRunner().Run();

            notifier.Received(1).ShowBalloon(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ToolTipIcon>(), Arg.Any<Action>());
            feed.Received(1).GetLatestReleaseJson();
        }

        [Fact]
        public void a_failing_tray_icon_does_not_escape_the_background_job()
        {
            feed.GetLatestReleaseJson().Returns(@"{""tag_name"":""v2.4.1""}");
            notifier.When(n => n.ShowBalloon(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ToolTipIcon>(), Arg.Any<Action>()))
                .Do(call => { throw new InvalidOperationException("no tray"); });

            CreateRunner().Run();

            logger.Received().Log(LogLevel.Warning, Arg.Any<string>(), Arg.Is<Exception>(ex => ex.Message == "no tray"));
        }

        [Fact]
        public void constructor_validates_its_arguments()
        {
            var checker = new UpdateChecker(feed, Substitute.For<IUpdateCheckHistory>(), Current, logger, () => now);
            Assert.Throws<ArgumentNullException>(() => new UpdateCheckRunner(null, Current, notifier, opened.Add, logger));
            Assert.Throws<ArgumentNullException>(() => new UpdateCheckRunner(checker, null, notifier, opened.Add, logger));
            Assert.Throws<ArgumentNullException>(() => new UpdateCheckRunner(checker, Current, null, opened.Add, logger));
            Assert.Throws<ArgumentNullException>(() => new UpdateCheckRunner(checker, Current, notifier, null, logger));
            Assert.Throws<ArgumentNullException>(() => new UpdateCheckRunner(checker, Current, notifier, opened.Add, null));
        }
    }

    public class SettingsUpdateCheckHistoryFacts
    {
        readonly InMemoryStorage storage = new InMemoryStorage();

        SettingsUpdateCheckHistory CreateHistory()
        {
            return new SettingsUpdateCheckHistory(new SettingsProvider(storage));
        }

        [Fact]
        public void there_is_no_last_check_at_first()
        {
            Assert.Null(CreateHistory().LastCheckUtc);
        }

        [Fact]
        public void remembers_the_last_check_across_restarts()
        {
            var when = new DateTime(2026, 3, 14, 10, 30, 15, 123, DateTimeKind.Utc);
            CreateHistory().RecordCheck(when);

            var afterRestart = CreateHistory().LastCheckUtc;

            Assert.Equal(when, afterRestart);
            Assert.Equal(DateTimeKind.Utc, afterRestart.Value.Kind);
        }

        [Fact]
        public void converts_local_times_to_utc()
        {
            var local = new DateTime(2026, 3, 14, 10, 30, 0, DateTimeKind.Local);
            CreateHistory().RecordCheck(local);

            Assert.Equal(local.ToUniversalTime(), CreateHistory().LastCheckUtc);
        }

        [Fact]
        public void reads_the_time_from_the_settings_file()
        {
            // proves that the file the history writes is the one it reads: same name, same format
            storage.Seed("UpdateCheckState", new Dictionary<string, string> { { "LastCheckUtc", "\"2026-03-14T10:30:15.1230000Z\"" } });

            Assert.Equal(new DateTime(2026, 3, 14, 10, 30, 15, 123, DateTimeKind.Utc), CreateHistory().LastCheckUtc);
        }

        [Fact]
        public void an_unreadable_value_counts_as_no_check()
        {
            storage.Seed("UpdateCheckState", new Dictionary<string, string> { { "LastCheckUtc", "\"yesterday\"" } });

            Assert.Null(CreateHistory().LastCheckUtc);
        }

        [Fact]
        public void is_stored_apart_from_the_preferences()
        {
            var provider = new SettingsProvider(storage);
            provider.GetSettings<PopupSettings>().FontSize = 33;   // an edit in the preferences window that was not saved yet
            var history = new SettingsUpdateCheckHistory(provider);

            history.RecordCheck(new DateTime(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc));

            // recording a check must not save the (possibly half-edited) preferences as a side effect
            Assert.True(storage.Contains("UpdateCheckState"));
            Assert.False(storage.Contains("PopupSettings"));
        }

        [Fact]
        public void requires_a_settings_provider()
        {
            Assert.Throws<ArgumentNullException>(() => new SettingsUpdateCheckHistory(null));
        }
    }
}
