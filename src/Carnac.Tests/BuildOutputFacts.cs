using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Carnac.Tests
{
    /// <summary>
    /// The application must be a single Carnac.exe (see installer\Carnac.iss): the dependencies are embedded by Costura,
    /// and the retired Squirrel updater with its helper libraries must not be part of it any more.
    /// </summary>
    public class BuildOutputFacts
    {
        static readonly Assembly App = typeof(Carnac.App).Assembly;

        static readonly string[] RetiredLibraries = { "squirrel", "deltacompression", "splat", "mono.cecil", "sharpziplib" };

        [Fact]
        public void carnac_does_not_reference_the_retired_updater_libraries()
        {
            var references = App.GetReferencedAssemblies().Select(name => name.Name.ToLowerInvariant()).ToArray();

            foreach (var retired in RetiredLibraries)
                Assert.False(references.Any(name => name.Contains(retired)), "Carnac still references " + retired);
        }

        [Fact]
        public void the_updater_libraries_are_not_embedded_in_the_executable()
        {
            var embedded = App.GetManifestResourceNames().Select(name => name.ToLowerInvariant()).ToArray();

            foreach (var retired in RetiredLibraries)
                Assert.False(embedded.Any(name => name.Contains(retired)), "an embedded resource still contains " + retired);
        }

        [Fact]
        public void costura_still_embeds_the_libraries_the_application_needs_at_runtime()
        {
            var embedded = App.GetManifestResourceNames().Select(name => name.ToLowerInvariant()).ToArray();

            foreach (var required in new[]
            {
                "costura.carnac.logic.dll", "costura.mahapps.metro.dll", "costura.settingsprovidernet.dll", "costura.yamldotnet.dll",
                "costura.system.reactive.core.dll", "costura.system.reactive.linq.dll", "costura.system.reactive.interfaces.dll",
                "costura.system.windows.interactivity.dll"
            })
            {
                Assert.True(embedded.Any(name => name.StartsWith(required, StringComparison.Ordinal)), "missing embedded " + required);
            }
        }
    }
}
