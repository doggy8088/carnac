## Carnac the Magnificent Keyboard Utility

[![Join the chat at https://gitter.im/Code52/carnac](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/Code52/carnac?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

A keyboard logging and presentation utility for presentations, screencasts, and to help you become a better keyboard user.

### Build Status

[![Build status](https://ci.appveyor.com/api/projects/status/qorhqwc2favf18r4?svg=true)](https://ci.appveyor.com/project/shiftkey/carnac)

### Installation

There are several options to install the latest version of Carnac on Windows.

#### [WinGet](https://learn.microsoft.com/en-us/windows/package-manager/winget/)

```powershell
winget install --id code52.Carnac
```

#### [Chocolatey](https://chocolatey.org/)

You can install the latest version of [Carnac](https://community.chocolatey.org/packages/carnac) via Chocolatey:

```powershell
choco install carnac
```

#### Manual setup

Alternatively, you can grab the latest zip file from [here](https://github.com/Code52/carnac/releases/latest), unpack it and run `Setup.exe`.

**Note:** Carnac requires .NET 4.5.2 to work - you can install that from [here](https://www.microsoft.com/en-au/download/details.aspx?id=42643) if you don't have it already.

### Updating

We use `Squirrel.Windows` to update your `carnac` application.

The application will check for updates in the background, if a new version has been released, it will automatically install the new version and once you restart `carnac` you will be up-to-date.

### Usage

#### Enabling silent mode

If you want to stop `Carnac` from recording certain key strokes, you can enter _silent mode_ by pressing `Ctrl+Alt+P`. To exit _silent mode_ you simply press `Ctrl+Alt+P` again.

#### Keymaps and process filter

Carnac names the shortcuts it knows: pressing `Ctrl+Shift+K` in VS Code shows `Ctrl + Shift + K [Delete Line]`. The names come from small YAML files in the `Keymaps` folder next to `Carnac.exe` (with the installer, usually `%LOCALAPPDATA%\Programs\Carnac\Keymaps`). Carnac reads them when it starts, so restart it after changing one. Copy a bundled file such as `chrome.yml` or `vscode.yml` as a starting point.

```yaml
group:   My Browsers
process: chrome|msedge

shortcuts:
  - name: Reopen closed tab
    keys:
      - Ctrl+Shift+T
  - name: Comment Selection
    keys:
      - Ctrl+K,Ctrl+C
```

- `process` is the process name **without `.exe`**. It is compared case-insensitively and in full: `Code` and `code` are the same, `chrome` does not match `chromedriver`. Give one name, several names separated by `|`, or a YAML list. Leave it empty to apply the keymap to every application.

    ```yaml
    process: chrome|msedge   # both browsers

    process:                 # the same, as a list
      - chrome
      - msedge
    ```

- `keys` lists the ways to trigger a shortcut. Write modifiers (`Ctrl`, `Alt`, `Shift`, `Win`) joined with `+`, then one key: a letter, a digit, or a key name such as `Enter`, `Escape`, `Back`, `Up`, `F5`, `PageDown` or `Oemcomma` (the names of [`System.Windows.Forms.Keys`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.keys)). The plus key is `Ctrl++`: like every character that is typed with Shift on a US keyboard (`!`, `?`, `_`, `{` ...), it means the shifted key, so `Ctrl++` is `Ctrl+Shift+=`. Write `Ctrl+=` or `Ctrl+Oemplus` for the unshifted key. The comma key after a modifier is `Ctrl+,`.
- A chord, several key presses one after the other, uses commas: `Ctrl+K,Ctrl+C`. Carnac holds back the first key of a possible chord; if the rest does not follow within about a second, that key is shown on its own.
- An entry Carnac cannot understand is skipped, and a message naming the file and shortcut goes to the Windows debug output (visible with a tool such as DebugView). Typical mistakes are `Ctrl+K Ctrl+C` (chords need a comma), `Up Arrow` (write `Up`) and `Esc` (write `Escape`).
- `src/Carnac.Logic/Keymaps/samples` holds opt-in keymaps that are not installed, for example the Konami code. To use one, copy it into the `Keymaps` folder and rename it to end in `.yml`.

To find a process name, open Task Manager, go to the **Details** tab and take the **Name** column without `.exe` (`Code.exe` is `Code`), or run `Get-Process | Select-Object -ExpandProperty Name` in PowerShell.

##### Process filter

*Preferences*, *Appearance*, *Process Filter* limits Carnac to some applications. It is a regular expression that is matched case-insensitively against the same process name (again without `.exe`) and may match a part of it. Leave it empty to show every application.

- `notepad|calc` shows keys from these applications only (and from any process whose name contains one of them).
- `^(notepad|calc)$` shows keys from exactly these two.
- `^(?!ZoomIt64$)` shows everything except ZoomIt. Do not write `^(?!ZoomIt64\.exe$)`: process names have no `.exe`, so that expression excludes nothing.

### Contributing

#### Getting started with Git and GitHub

- [Setting up Git for Windows and connecting to GitHub](http://help.github.com/win-set-up-git/)
- [Forking a GitHub repository](http://help.github.com/fork-a-repo/)
- [The simple guide to GIT guide](http://rogerdudler.github.com/git-guide/)
- [Open an issue](https://github.com/Code52/carnac/issues) if you encounter a bug or have a suggestion for improvements/features

Once you're familiar with Git and GitHub, clone the repository and run `.\build.ps1 -Target Run-Unit-Tests -Configuration Release` to compile the code and run the unit tests on Windows.

### Resources

This blog series covers a series of refactorings which have recently happened in Carnac to make better use of Rx.
If you are learning Rx and want to be shown through Carnac's codebase then this blog series may help you.

1. [Part 1 - Refactoring the InterceptKeys class](http://jake.ginnivan.net/blog/carnac-improvements/part-1/)
1. [Part 2 - Refactoring the MessageProvider class](http://jake.ginnivan.net/blog/carnac-improvements/part-2/)
1. [Part 3 - Introducing the MessageController class](http://jake.ginnivan.net/blog/carnac-improvements/part-3/)

### Install Carnac with ClickOnce (Auto-update)

- https://github.com/doggy8088/carnac/releases

### Publish a new version using ClickOnce

Steps to setup dev environment:

1. `cd /D G:\Projects`
2. `git clone https://github.com/doggy8088/carnac.git carnac -b dev`
3. `git clone https://github.com/doggy8088/carnac.git carnac-publish -b gh-pages`
4. `cd carnac-publish`
5. `git config --local core.autocrlf false` (Important!!)
6. `cd ..\carnac\src`
5. Open `Carnac.sln` in Visual Studio 2017

Steps to publish a new version

1. Modify some code
2. Build / Test
3. Publish
    1. Hit `Publish` in `Carnac` project in the Solution Explorer. ( **You MUST be publish first to increment the Publish version number automatically.** )
    2. `git add .`
    3. `git commit -m "Write Some Release Notes"`
    4. `git push`
    2. `cd ..\..\carnac-publish`
    3. `git add .`
    4. `git commit -m "Carnac_1_0_0_X"`
    5. `git push`

### Publish a new version to WinGet

The `doggy8088.Carnac` WinGet package installs the per-user Inno Setup installer from `installer/Carnac.iss`, which the [Release workflow](.github/workflows/release.yml) builds and publishes to GitHub Releases.

1. Tag the release on `dev` and push the tag:

    ```powershell
    git tag v2.4.1
    git push origin v2.4.1
    ```

    The workflow builds and tests Carnac, compiles the installer and publishes `Carnac-2.4.1-Setup.exe` with its SHA256 checksum.
2. Submit the new version to [winget-pkgs](https://github.com/microsoft/winget-pkgs) with [wingetcreate](https://github.com/microsoft/winget-create):

    ```powershell
    wingetcreate update doggy8088.Carnac --version 2.4.1 --urls https://github.com/doggy8088/carnac/releases/download/v2.4.1/Carnac-2.4.1-Setup.exe --submit
    ```

To build the installer locally, build `src\Carnac.sln` in Release with the MSBuild flags from the workflow's Build step, then run `iscc /DAppVersion=2.4.1 installer\Carnac.iss`.
