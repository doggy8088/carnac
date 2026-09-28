using System;
using System.Diagnostics;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    /// <summary>
    /// The Windows calls behind the elevation detection, exercised on the test process itself and on the foreground window.
    /// These are read-only queries; nothing is started, elevated or injected.
    /// </summary>
    public class SystemElevationCheckerFacts
    {
        readonly SystemElevationChecker sut = new SystemElevationChecker();

        [Fact]
        public void the_token_of_a_process_and_the_process_itself_agree_about_the_current_process()
        {
            using (var current = Process.GetCurrentProcess())
            {
                var byProcessId = sut.IsProcessElevated(current.Id);

                Assert.NotNull(byProcessId);
                Assert.Equal(sut.IsCurrentProcessElevated, byProcessId.Value);
            }
        }

        [Fact]
        public void a_process_that_does_not_exist_is_neither_elevated_nor_not_elevated()
        {
            Assert.Null(sut.IsProcessElevated(int.MaxValue));
        }

        [Fact]
        public void the_idle_process_id_cannot_be_told()
        {
            Assert.Null(sut.IsProcessElevated(0));
        }

        [Fact]
        public void asking_for_the_foreground_process_never_throws()
        {
            var foreground = new SystemForegroundSource().GetForegroundProcess();

            // there is none when the tests run without a desktop
            if (foreground != null)
            {
                Assert.True(foreground.Id > 0);
                Assert.False(string.IsNullOrEmpty(foreground.Name));
            }
        }
    }
}
