using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Carnac.Logic.Native;
using Carnac.UI;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Shouldly;
using Xunit;
using Message = Carnac.Logic.Models.Message;
using SettingsProviderNet;
using Carnac.Tests.ViewModels;

namespace Carnac.Tests
{
    public class KeysControllerFacts
    {
        const int MessageAOnNextTick = 100;

        readonly ObservableCollection<Message> messages = new ObservableCollection<Message>();
        readonly TestScheduler testScheduler;
        readonly Message messageA = new Message(A);
        readonly PopupSettings popupSettings = new PopupSettings();

        public KeysControllerFacts()
        {
            testScheduler = new TestScheduler();
            popupSettings.ItemFadeDelay = GetDefaultFadeDelay(popupSettings);
        }

        KeysController CreateKeysController(IObservable<Message> messageStream)
        {
            var settingsService = Substitute.For<ISettingsProvider>();
            settingsService.GetSettings<PopupSettings>().Returns(popupSettings);

            return CreateKeysController(messageStream, settingsService);
        }

        KeysController CreateKeysController(IObservable<Message> messageStream, ISettingsProvider settingsService)
        {
            var messageProvider = Substitute.For<IMessageProvider>();
            messageProvider.GetMessageStream().Returns(_ => messageStream);
            var concurrencyService = Substitute.For<IConcurrencyService>();
            concurrencyService.MainThreadScheduler.Returns(testScheduler);
            concurrencyService.Default.Returns(testScheduler);

            return new KeysController(messages, messageProvider, concurrencyService, settingsService);
        }

        [Fact]
        public void MessagesAreAddedIntoKeysColletion()
        {
            var sut = CreateKeysController(SingleMessageAt100Ticks());
            sut.Start();
            testScheduler.AdvanceTo(MessageAOnNextTick + 1);  //+1 for the cost of the ObserveOn scheduling

            messages.ShouldContain(messageA);
            messageA.IsDeleting.ShouldBe(false);
        }

        [Fact]
        public void MessagesAreFlaggedAsDeletingAfter5Seconds()
        {
            var sut = CreateKeysController(SingleMessageAt100Ticks());
            sut.Start();
            testScheduler.AdvanceBy(MessageAOnNextTick + 1);
            testScheduler.AdvanceBy(5.Seconds());

            Assert.Single(messages);
            messages.Single().ShouldBe(messageA.FadeOut());
        }

        [Fact]
        public void MessagesIsRemovedAfter6Seconds()
        {
            var sut = CreateKeysController(SingleMessageAt100Ticks());
            sut.Start();
            testScheduler.AdvanceBy(MessageAOnNextTick + 2);
            testScheduler.AdvanceBy(6.Seconds());

            messages.ShouldBeEmpty();
        }

        [Fact]
        public void MessageTimeoutIsStartedAgainIfMessageIsUpdated()
        {
            var expected = messageA.Merge(new Message(A));
            var messageSequence = testScheduler.CreateColdObservable(
                ReactiveTest.OnNext(MessageAOnNextTick, messageA),
                ReactiveTest.OnNext(3.Seconds(), expected)
                );

            var sut = CreateKeysController(messageSequence);

            sut.Start();
            testScheduler.AdvanceBy(6.Seconds());

            messages.Single().IsDeleting.ShouldBe(false);
            messages.Single().ShouldBe(expected);
        }

        [Fact]
        public void MultiMerge()
        {
            var message1 = new Message(Down);
            var message2 = message1.Merge(new Message(Down));
            var message3 = message2.Merge(new Message(Down));

            var expected = message3;
            var messageSequence = testScheduler.CreateColdObservable(
                ReactiveTest.OnNext(0.1.Seconds(), message1),
                ReactiveTest.OnNext(0.2.Seconds(), message2),
                ReactiveTest.OnNext(0.3.Seconds(), message3)
                );

            var sut = CreateKeysController(messageSequence);

            sut.Start();
            testScheduler.AdvanceBy(1.Seconds());

            messages.Single().IsDeleting.ShouldBe(false);
            messages.Single().ShouldBe(expected);
        }

        [Fact]
        public void MultiMergeOfARepeatedShortcutKeepsASinglePopup()
        {
            var message1 = new Message(CtrlDown);
            var message2 = Message.MergeIfNeeded(message1, new Message(CtrlDown), RepeatedKeyPolicy.Default);
            var message3 = Message.MergeIfNeeded(message2, new Message(CtrlDown), RepeatedKeyPolicy.Default);
            var messageSequence = testScheduler.CreateColdObservable(
                ReactiveTest.OnNext(0.1.Seconds(), message1),
                ReactiveTest.OnNext(0.2.Seconds(), message2),
                ReactiveTest.OnNext(0.3.Seconds(), message3)
                );

            var sut = CreateKeysController(messageSequence);

            sut.Start();
            testScheduler.AdvanceBy(1.Seconds());

            messages.Single().IsDeleting.ShouldBe(false);
            messages.Single().ShouldBe(message3);
            Assert.Equal("Ctrl + ↓ x 3 ", string.Join(string.Empty, messages.Single().Text));
        }

        static KeyPress CtrlDown
        {
            get
            {
                return new KeyPress(new ProcessInfo("foo"),
                    new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, true, false), false, new[] { "Ctrl", "Down" });
            }
        }

        [Fact]
        public void FadeDelayChangedAfterStartIsUsedForTheNextMessage()
        {
            var messageB = new Message(Down);
            var messageSequence = testScheduler.CreateColdObservable(
                ReactiveTest.OnNext(MessageAOnNextTick, messageA),
                ReactiveTest.OnNext(10.Seconds(), messageB)
                );
            var sut = CreateKeysController(messageSequence);
            sut.Start();
            testScheduler.AdvanceBy(1.Seconds());

            popupSettings.ItemFadeDelay = 2;

            // Message A is already on screen and keeps the schedule of the old delay (5 seconds).
            testScheduler.AdvanceTo(4.Seconds());
            messages.Single().IsDeleting.ShouldBe(false);
            testScheduler.AdvanceTo(5.5.Seconds());
            messages.Single().IsDeleting.ShouldBe(true);
            testScheduler.AdvanceTo(9.Seconds());
            messages.ShouldBeEmpty();

            // Message B arrives at 10 seconds and fades after the new delay (2 seconds), not after the old one.
            testScheduler.AdvanceTo(10.Seconds() + 10);
            messages.Single().IsDeleting.ShouldBe(false);
            testScheduler.AdvanceTo(11.5.Seconds());
            messages.Single().IsDeleting.ShouldBe(false);
            testScheduler.AdvanceTo(12.5.Seconds());
            messages.Single().IsDeleting.ShouldBe(true);
        }

        [Fact]
        public void FadeDelayIsReadAgainForEveryMessage()
        {
            var messageB = new Message(Down);
            var messageC = new Message(A);
            var messageSequence = testScheduler.CreateColdObservable(
                ReactiveTest.OnNext(1.Seconds(), messageA),
                ReactiveTest.OnNext(20.Seconds(), messageB),
                ReactiveTest.OnNext(40.Seconds(), messageC)
                );
            var sut = CreateKeysController(messageSequence);
            sut.Start();

            // Message A arrives at 1 second and gets a delay of 10 seconds.
            popupSettings.ItemFadeDelay = 10;
            testScheduler.AdvanceTo(10.5.Seconds());
            messages.Single().IsDeleting.ShouldBe(false);
            testScheduler.AdvanceTo(11.5.Seconds());
            messages.Single().IsDeleting.ShouldBe(true);

            // Message B arrives at 20 seconds and gets a delay of 3 seconds.
            popupSettings.ItemFadeDelay = 3;
            testScheduler.AdvanceTo(22.5.Seconds());
            messages.Single().IsDeleting.ShouldBe(false);
            testScheduler.AdvanceTo(23.5.Seconds());
            messages.Single().IsDeleting.ShouldBe(true);

            // Message C arrives at 40 seconds and gets a delay of 8 seconds.
            popupSettings.ItemFadeDelay = 8;
            testScheduler.AdvanceTo(47.Seconds());
            messages.Single().IsDeleting.ShouldBe(false);
            testScheduler.AdvanceTo(48.5.Seconds());
            messages.Single().IsDeleting.ShouldBe(true);
        }

        [Fact]
        public void FadeDelaySavedThroughTheSharedSettingsProviderIsUsedForTheNextMessage()
        {
            var settingsProvider = new SettingsProvider(new InMemorySettingsStorage());
            var sut = CreateKeysController(SingleMessageAt100Ticks(), settingsProvider);
            sut.Start();

            // This is what the Preferences window does: edit the settings it got from the same provider and save them.
            var preferences = CreatePreferencesViewModel(settingsProvider);
            preferences.Settings.ItemFadeDelay = 2;
            preferences.SaveCommand.Execute(null);

            testScheduler.AdvanceTo(MessageAOnNextTick + 1);
            messages.Single().IsDeleting.ShouldBe(false);
            testScheduler.AdvanceTo(2.5.Seconds());
            messages.Single().IsDeleting.ShouldBe(true);
        }

        [Fact]
        public void FadeDelayResetToDefaultsInPreferencesIsUsedForTheNextMessage()
        {
            var settingsProvider = new SettingsProvider(new InMemorySettingsStorage());
            var sut = CreateKeysController(SingleMessageAt100Ticks(), settingsProvider);
            sut.Start();

            var preferences = CreatePreferencesViewModel(settingsProvider);
            preferences.Settings.ItemFadeDelay = 2;
            preferences.SaveCommand.Execute(null);
            preferences.ResetToDefaultsCommand.Execute(null);

            testScheduler.AdvanceTo(3.Seconds());
            messages.Single().IsDeleting.ShouldBe(false);
            testScheduler.AdvanceTo(5.5.Seconds());
            messages.Single().IsDeleting.ShouldBe(true);
        }

        [Fact]
        public void FadeDelayOfZeroIsRaisedToOneSecond()
        {
            AssertMessageFadesAfter(configuredSeconds: 0, expectedSeconds: 1);
        }

        [Fact]
        public void NegativeFadeDelayIsRaisedToOneSecond()
        {
            AssertMessageFadesAfter(configuredSeconds: -5, expectedSeconds: 1);
        }

        [Fact]
        public void FadeDelayAboveTheSliderRangeIsLimitedToFiftySeconds()
        {
            AssertMessageFadesAfter(configuredSeconds: 1000, expectedSeconds: 50);
        }

        [Fact]
        public void InfiniteFadeDelayIsLimitedToFiftySeconds()
        {
            AssertMessageFadesAfter(configuredSeconds: double.PositiveInfinity, expectedSeconds: 50);
        }

        [Fact]
        public void FadeDelayThatIsNotANumberFallsBackToTheDefault()
        {
            AssertMessageFadesAfter(configuredSeconds: double.NaN, expectedSeconds: 5);
        }

        [Fact]
        public void FadeDelayInsideTheSliderRangeIsUsedAsIs()
        {
            AssertMessageFadesAfter(configuredSeconds: 12, expectedSeconds: 12);
        }

        [Fact]
        public void FadeOutDelayIsClampedToTheSliderRange()
        {
            KeysController.GetFadeOutDelay(0).ShouldBe(TimeSpan.FromSeconds(1));
            KeysController.GetFadeOutDelay(0.5).ShouldBe(TimeSpan.FromSeconds(1));
            KeysController.GetFadeOutDelay(1).ShouldBe(TimeSpan.FromSeconds(1));
            KeysController.GetFadeOutDelay(7.5).ShouldBe(TimeSpan.FromSeconds(7.5));
            KeysController.GetFadeOutDelay(50).ShouldBe(TimeSpan.FromSeconds(50));
            KeysController.GetFadeOutDelay(50.5).ShouldBe(TimeSpan.FromSeconds(50));
            KeysController.GetFadeOutDelay(double.NegativeInfinity).ShouldBe(TimeSpan.FromSeconds(1));
            KeysController.GetFadeOutDelay(double.PositiveInfinity).ShouldBe(TimeSpan.FromSeconds(50));
        }

        [Fact]
        public void FallbackFadeDelayIsTheDefaultOfTheSetting()
        {
            KeysController.DefaultFadeDelaySeconds.ShouldBe(GetDefaultFadeDelay(new PopupSettings()));
            KeysController.GetFadeOutDelay(double.NaN).ShouldBe(TimeSpan.FromSeconds(KeysController.DefaultFadeDelaySeconds));
        }

        void AssertMessageFadesAfter(double configuredSeconds, double expectedSeconds)
        {
            popupSettings.ItemFadeDelay = configuredSeconds;
            var sut = CreateKeysController(SingleMessageAt100Ticks());
            sut.Start();

            testScheduler.AdvanceTo(expectedSeconds.Seconds());
            messages.Single().IsDeleting.ShouldBe(false);

            testScheduler.AdvanceTo(expectedSeconds.Seconds() + MessageAOnNextTick + 10);
            messages.Single().IsDeleting.ShouldBe(true);
        }

        PreferencesViewModel CreatePreferencesViewModel(ISettingsProvider settingsProvider)
        {
            var screenManager = Substitute.For<IScreenManager>();
            screenManager.GetScreens().Returns(new[] { new DetailedScreen { Index = 1, Width = 1920, Height = 1080 } });
            return new PreferencesViewModel(settingsProvider, screenManager);
        }

        static KeyPress A
        {
            get
            {
                return new KeyPress(new ProcessInfo("foo"),
                    new InterceptKeyEventArgs(Keys.A, KeyDirection.Down, false, false, false), false, new[] { "a" });
            }
        }

        static KeyPress Down
        {
            get
            {
                return new KeyPress(new ProcessInfo("foo"),
                    new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, false, false), false, new[] { "Down" });
            }
        }

        IObservable<Message> SingleMessageAt100Ticks()
        {
            return testScheduler.CreateColdObservable(
               ReactiveTest.OnNext(MessageAOnNextTick, messageA)
               );
        }

        double GetDefaultFadeDelay(PopupSettings settings)
        {
            AttributeCollection attributes =
                TypeDescriptor.GetProperties(settings)["ItemFadeDelay"].Attributes;
            DefaultValueAttribute myAttribute =
                (DefaultValueAttribute)attributes[typeof(DefaultValueAttribute)];

            double fadeDelay;
            double.TryParse(myAttribute.Value.ToString(), out fadeDelay);
            return fadeDelay;
        }

    }
}
