# Contributing

## Translating Carnac

The Preferences window and the tray menu are translated with resource files in
[`src/Carnac/Properties`](src/Carnac/Properties). `Resources.resx` holds the English texts, and
`Resources.zh-TW.resx` and `Resources.zh-CN.resx` are the Traditional and Simplified Chinese translations.
Carnac follows the display language of Windows; **General > Language** overrides it. Windows that are
already open keep their language, so reopen Preferences to see a change there. The tray menu and the key
labels change at once.

To add a language (for example German, `de-DE`):

1. Copy `Resources.resx` to `Resources.de-DE.resx` in the same folder and translate every `<value>`.
   Keep the `name` of each entry; an entry that is missing or empty makes a test fail.
2. Add the file to `src/Carnac/Carnac.csproj` next to the other translations:

   ```xml
   <EmbeddedResource Include="Properties\Resources.de-DE.resx">
     <DependentUpon>Resources.resx</DependentUpon>
   </EmbeddedResource>
   ```

3. Add the culture name to `UiLanguages.Codes` in `src/Carnac.Logic/UiLanguages.cs`, and its name in its own
   language to `LanguageOption.GetNativeName` in `src/Carnac/UI/LanguageOption.cs`.
4. Add the satellite assembly (`de-DE\Carnac.resources.dll`) to the `[Files]` section of `installer/Carnac.iss`.
   Anything else that packages the application has to copy the `<culture>` folders next to `Carnac.exe` as well.
5. Run the unit tests. `LocalizationResourceFacts` checks that every key of `Resources.resx` exists in each
   translation, that nothing is left empty, and that the XAML only uses keys that exist.

New texts in the user interface go into `Resources.resx` first and are bound in XAML with
`{x:Static properties:Resources.Some_Key}` (`xmlns:properties="clr-namespace:Carnac.Properties"`), so a missing
key is an error when the window is created instead of a blank label. Visual Studio regenerates
`Resources.Designer.cs`; without it, generate the file with `StronglyTypedResourceBuilder`
(public class, namespace `Carnac.Properties`), the tests notice when it is out of date.

### Key labels

The keymaps and the messages use English key names such as `Ctrl` or `Delete`. `KeyLabels`
(`src/Carnac.Logic/KeyLabels.cs`) translates the few labels that differ, for example `Ctrl` to `Strg` for German,
when the key is displayed. Languages without an entry show the English names. To add labels for a language,
add its two-letter code and the labels that differ to the table in that file.
