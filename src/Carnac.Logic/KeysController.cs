using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Carnac.Logic.Models;
using SettingsProviderNet;

namespace Carnac.Logic
{
    public class KeysController : IDisposable
    {
        // The allowed range of the fade delay: the "Popup Fade Delay (sec)" slider of the Appearance tab.
        public const double MinFadeDelaySeconds = 1;
        public const double MaxFadeDelaySeconds = 50;

        // Used when the setting is not a number. Same as the [DefaultValue] of PopupSettings.ItemFadeDelay.
        public const double DefaultFadeDelaySeconds = 5;

        static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);
        readonly PopupSettings settings;
        readonly ObservableCollection<Message> messages;
        readonly IMessageProvider messageProvider;
        readonly IConcurrencyService concurrencyService;
        readonly SingleAssignmentDisposable actionSubscription = new SingleAssignmentDisposable();

        public KeysController(ObservableCollection<Message> messages, IMessageProvider messageProvider, IConcurrencyService concurrencyService, ISettingsProvider settingsProvider)
        {
            this.messages = messages;
            this.messageProvider = messageProvider;
            this.concurrencyService = concurrencyService;

            // SettingsProvider caches the settings, so this is the instance the Preferences window edits and saves.
            // It is kept (instead of copying the delay) so that the delay is read again for every message.
            settings = settingsProvider.GetSettings<PopupSettings>();
        }

        /// <summary>
        /// Converts the configured fade delay to the delay that is used: clamped to the range of the
        /// Preferences slider, so a typed 0, negative, infinite or NaN value cannot produce an instant or invalid timer.
        /// </summary>
        public static TimeSpan GetFadeOutDelay(double configuredSeconds)
        {
            if (double.IsNaN(configuredSeconds))
                configuredSeconds = DefaultFadeDelaySeconds;

            return TimeSpan.FromSeconds(Math.Max(MinFadeDelaySeconds, Math.Min(MaxFadeDelaySeconds, configuredSeconds)));
        }

        public void Start()
        {
            var messageStream = messageProvider.GetMessageStream().Publish();

            var addMessageSubscription = messageStream
                .ObserveOn(concurrencyService.MainThreadScheduler)
                .Subscribe(newMessage =>
                    {
                        if (newMessage.Previous != null)
                        {
                            messages.Remove(newMessage.Previous);
                        }
                        messages.Add(newMessage);
                    });

            // The delay is taken from the settings when a message arrives, so a changed setting applies to
            // the next popup without a restart. Popups that are already on screen keep their schedule.
            // (The value is written by the UI thread and read on the thread of the message stream; the clamp
            // makes any value that is read safe to use.)
            var fadeOutMessageSeq = messageStream
                .SelectMany(m => Observable.Timer(GetFadeOutDelay(settings.ItemFadeDelay), concurrencyService.Default).Select(_ => m))
                .Select(m => m.FadeOut())
                .Publish();

            var fadeOutMessageSubscription = fadeOutMessageSeq
                .ObserveOn(concurrencyService.MainThreadScheduler)
                .Subscribe(msg =>
                {
                    var idx = messages.IndexOf(msg.Previous);
                    if(idx>-1)
                        messages[idx] = msg;
                });

            // Finally we just put a one second delay on the messages from the fade out stream and flag to remove.
            var removeMessageSubscription = fadeOutMessageSeq
                .Delay(OneSecond, concurrencyService.Default)
                .ObserveOn(concurrencyService.MainThreadScheduler)
                .Subscribe(msg => messages.Remove(msg));


            actionSubscription.Disposable = new CompositeDisposable(
                addMessageSubscription,
                fadeOutMessageSubscription,
                removeMessageSubscription,
                fadeOutMessageSeq.Connect(),
                messageStream.Connect());
        }

        public void Dispose()
        {
            actionSubscription.Dispose();
        }
    }
}