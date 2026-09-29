using System;
using System.Globalization;
using Carnac.Logic.Models;
using SettingsProviderNet;

namespace Carnac.Logic
{
    /// <summary>Stores the time of the last update check in its own small settings file (see <see cref="UpdateCheckState"/>).</summary>
    public sealed class SettingsUpdateCheckHistory : IUpdateCheckHistory
    {
        const string Format = "o";

        readonly ISettingsProvider settingsProvider;

        public SettingsUpdateCheckHistory(ISettingsProvider settingsProvider)
        {
            if (settingsProvider == null)
                throw new ArgumentNullException("settingsProvider");

            this.settingsProvider = settingsProvider;
        }

        public DateTime? LastCheckUtc
        {
            get
            {
                DateTime last;
                var text = settingsProvider.GetSettings<UpdateCheckState>().LastCheckUtc;
                return DateTime.TryParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out last)
                    ? last.ToUniversalTime()
                    : (DateTime?)null;
            }
        }

        public void RecordCheck(DateTime utcNow)
        {
            var state = settingsProvider.GetSettings<UpdateCheckState>();
            state.LastCheckUtc = utcNow.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture);
            settingsProvider.SaveSettings(state);
        }
    }
}
