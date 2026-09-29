# Contributing

## Translating Carnac

The Preferences window and the tray menu are translated with resource files in
[`src/Carnac/Properties`](src/Carnac/Properties). `Resources.resx` holds the English texts, and
`Resources_zh-TW.resx` and `Resources_zh-CN.resx` are the Traditional and Simplified Chinese translations.
Carnac follows the display language of Windows; **General > Language** overrides it. Windows that are
already open keep their language, so reopen Preferences to see a change there. The tray menu and the key
labels change at once.

The translations are compiled into `Carnac.exe` itself: there are no satellite assemblies or extra folders to
ship. That is why the files are called `Resources_<culture>.resx` and not `Resources.<culture>.resx`: MSBuild turns
the second form into a satellite assembly, which needs `AL.exe` and is not embedded by Costura. Do not rename them.
`EmbeddedResourceManager` looks a translation up by that name, falls back to the parent culture and then to English,
and uses the English text for a key that a translation does not have.

To add a language (for example German, `de-DE`):

1. Copy `Resources.resx` to `Resources_de-DE.resx` in the same folder and translate every `<value>`.
   Keep the `name` of each entry; an entry that is missing or empty makes a test fail.
2. Add the file to `src/Carnac/Carnac.csproj` next to the other translations, as a plain embedded resource:

   ```xml
   <EmbeddedResource Include="Properties\Resources_de-DE.resx" />
   ```

3. Add the culture name to `UiLanguages.Codes` in `src/Carnac.Logic/UiLanguages.cs`, and its name in its own
   language to `LanguageOption.GetNativeName` in `src/Carnac/UI/LanguageOption.cs`.
4. Run the unit tests. `LocalizationResourceFacts` checks that every key of `Resources.resx` exists in each
   translation, that nothing is left empty, that every language in `UiLanguages.Codes` has its resource embedded,
   that no satellite assemblies are built, and that the XAML only uses keys that exist.

New texts in the user interface go into `Resources.resx` first and are bound in XAML with
`{x:Static properties:Resources.Some_Key}` (`xmlns:properties="clr-namespace:Carnac.Properties"`), so a missing
key is an error when the window is created instead of a blank label. Add the matching property to
`src/Carnac/Properties/Resources.cs` (it is written by hand, not generated) and the text to every translation;
the tests notice when a key or a property is missing.

### Key labels

The keymaps and the messages use English key names such as `Ctrl` or `Delete`. `KeyLabels`
(`src/Carnac.Logic/KeyLabels.cs`) translates the few labels that differ, for example `Ctrl` to `Strg` for German,
when the key is displayed. Languages without an entry show the English names. To add labels for a language,
add its two-letter code and the labels that differ to the table in that file.
