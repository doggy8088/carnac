using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Carnac.Properties
{
    /// <summary>
    /// A <see cref="ResourceManager"/> that reads the translations from resources embedded in the assembly itself
    /// instead of from satellite assemblies.
    /// </summary>
    /// <remarks>
    /// The English texts are the normal neutral resources (<c>Carnac.Properties.Resources.resources</c>, from Resources.resx).
    /// The translation for a culture is the culture-neutral embedded resource <c>&lt;base name&gt;_&lt;culture&gt;.resources</c>
    /// (<c>Carnac.Properties.Resources_zh-TW.resources</c>, from Resources_zh-TW.resx). A culture without its own resource uses
    /// the one of its parent culture and finally English, and a key that a translation does not have is looked up in
    /// the parents and in English as well, exactly as with satellite assemblies.
    /// Satellite assemblies are avoided because building them needs AL.exe, which not every build machine has, and
    /// because Costura does not embed the translations of the application itself.
    /// </remarks>
    public sealed class EmbeddedResourceManager : ResourceManager
    {
        // Culture name -> the set embedded for exactly that culture, or null when there is none.
        readonly Dictionary<string, ResourceSet> ownSets = new Dictionary<string, ResourceSet>();

        public EmbeddedResourceManager(string baseName, Assembly assembly)
            : base(baseName, assembly)
        {
        }

        /// <summary>
        /// The name of the embedded resource that holds the translation for the culture.
        /// </summary>
        public string GetResourceName(CultureInfo culture)
        {
            return BaseName + "_" + culture.Name + ".resources";
        }

        protected override ResourceSet InternalGetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents)
        {
            for (var current = culture; current != null && current.Name.Length > 0; current = current.Parent)
            {
                var set = GetOwnResourceSet(current, createIfNotExists);
                if (set != null)
                    return set;

                if (!tryParents)
                    return null;
            }

            // English (the invariant culture): the neutral resources of the main assembly.
            return base.InternalGetResourceSet(CultureInfo.InvariantCulture, createIfNotExists, tryParents);
        }

        ResourceSet GetOwnResourceSet(CultureInfo culture, bool createIfNotExists)
        {
            lock (ownSets)
            {
                ResourceSet set;
                if (ownSets.TryGetValue(culture.Name, out set))
                    return set;

                if (!createIfNotExists)
                    return null;

                // The stream is owned by the set, which is kept for the lifetime of the application.
                var stream = MainAssembly.GetManifestResourceStream(GetResourceName(culture));
                set = stream == null ? null : new ResourceSet(stream);
                ownSets.Add(culture.Name, set);
                return set;
            }
        }

        public override void ReleaseAllResources()
        {
            lock (ownSets)
            {
                foreach (var set in ownSets.Values)
                {
                    if (set != null)
                        set.Close();
                }
                ownSets.Clear();
            }

            base.ReleaseAllResources();
        }
    }
}
