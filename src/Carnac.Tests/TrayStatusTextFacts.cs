using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    public class TrayStatusTextFacts
    {
        static readonly string Dash = " " + (char)0x2013 + " ";
        static readonly string Ellipsis = ((char)0x2026).ToString();

        [Fact]
        public void is_just_the_app_name_while_everything_is_active()
        {
            var text = TrayStatusText.Compose("Carnac", false, false, "paused", "silent mode");

            Assert.Equal("Carnac", text);
        }

        [Fact]
        public void appends_paused()
        {
            var text = TrayStatusText.Compose("Carnac", true, false, "paused", "silent mode");

            Assert.Equal("Carnac" + Dash + "paused", text);
        }

        [Fact]
        public void appends_silent_mode()
        {
            var text = TrayStatusText.Compose("Carnac", false, true, "paused", "silent mode");

            Assert.Equal("Carnac" + Dash + "silent mode", text);
        }

        [Fact]
        public void appends_both_states()
        {
            var text = TrayStatusText.Compose("Carnac", true, true, "paused", "silent mode");

            Assert.Equal("Carnac" + Dash + "paused, silent mode", text);
        }

        [Fact]
        public void never_exceeds_the_notify_icon_limit()
        {
            var text = TrayStatusText.Compose("Carnac", true, true, new string('p', 40), new string('s', 40));

            Assert.Equal(TrayStatusText.MaxLength, text.Length);
            Assert.True(text.StartsWith("Carnac" + Dash));
            Assert.True(text.EndsWith(Ellipsis));
        }

        [Fact]
        public void keeps_texts_that_fit_exactly_untouched()
        {
            var name = new string('x', TrayStatusText.MaxLength);

            Assert.Equal(name, TrayStatusText.Compose(name, false, false, "paused", "silent mode"));
        }

        [Fact]
        public void tolerates_missing_texts()
        {
            Assert.Equal(string.Empty, TrayStatusText.Compose(null, false, false, null, null));
            Assert.Equal("Carnac", TrayStatusText.Compose("Carnac", true, true, null, string.Empty));
        }
    }
}
