using System.ComponentModel;

namespace Carnac.Logic.Models
{
    /// <summary>
    /// What the update check remembers between runs. Kept apart from <see cref="PopupSettings"/> on purpose: the preferences window
    /// edits and saves those, and a background check must not save half-edited preferences as a side effect.
    /// </summary>
    public class UpdateCheckState
    {
        /// <summary>When the last check was started, as a round-trip ("o") UTC time; empty if there was none.</summary>
        [DefaultValue("")]
        public string LastCheckUtc { get; set; }
    }
}
