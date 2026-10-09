## Carnac the Magnificent Keyboard Utility

[![Join the chat at https://gitter.im/Code52/carnac](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/Code52/carnac?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

A keyboard logging and presentation utility for presentations, screencasts, and to help you become a better keyboard user.

### Build Status

[![Build status](https://ci.appveyor.com/api/projects/status/qorhqwc2favf18r4?svg=true)](https://ci.appveyor.com/project/shiftkey/carnac)

### Installation

There are several options to install the latest version of Carnac on Windows.

#### [WinGet](https://learn.microsoft.com/en-us/windows/package-manager/winget/)

```powershell
winget install --id doggy8088.Carnac
```

#### [Chocolatey](https://chocolatey.org/)

You can install the latest version of [Carnac](https://community.chocolatey.org/packages/carnac) via Chocolatey:

```powershell
choco install carnac
```

#### Portable zip

Every [GitHub release](https://github.com/doggy8088/carnac/releases/latest) also contains `Carnac-<version>-portable.zip`, a copy of Carnac that needs no installation (handy on locked-down PCs, USB sticks or for a quick trial). Extract it anywhere and run `Carnac.exe`. It holds the same files the installer copies: `Carnac.exe`, `Carnac.exe.config`, the `Keymaps` folder and `LICENSE.md`.

- Carnac needs .NET Framework 4.5.2 or later; the installer checks that for you, the zip does not.
- Settings are still stored in `%APPDATA%\Carnac`, shared with an installed copy of Carnac. Delete that folder to remove them.
- A portable copy does not update itself: download the newer zip from the releases page.
- If Windows SmartScreen blocks the extracted `Carnac.exe`, unblock the zip first (right-click, Properties, Unblock) and extract it again.
- `sha256sums.txt` on the release lists the SHA256 hash of the installer and of the zip. To check a download: `Get-FileHash .\Carnac-<version>-portable.zip -Algorithm SHA256` in PowerShell, or `sha256sum -c --ignore-missing sha256sums.txt` on Linux, macOS and WSL.

#### Manual setup

Alternatively, you can grab the latest zip file from [here](https://github.com/Code52/carnac/releases/latest), unpack it and run `Setup.exe`.

**Note:** Carnac requires .NET 4.5.2 to work - you can install that from [here](https://www.microsoft.com/en-au/download/details.aspx?id=42643) if you don't have it already.

### Updating

Carnac does not update itself and never installs anything on its own. Update it the way you installed it:

- **WinGet:** `winget upgrade doggy8088.Carnac`
- **Chocolatey:** `choco upgrade carnac`
- **Manual setup / zip:** download the newest release from the [releases page](https://github.com/doggy8088/carnac/releases/latest).

If you would like to be told when a new version is out, switch on **Check for updates on startup** in the settings (**General** tab, off by default). Carnac then asks GitHub (`api.github.com`) at most once a day whether a newer release exists and shows a notice at the tray icon; clicking it opens the release page. Nothing is downloaded, and while the option is off Carnac makes no network requests at all. If the check fails (for example when you are offline) it is only written to the [log](#troubleshooting).

### Usage

#### Enabling silent mode

If you want to stop `Carnac` from recording certain key strokes, you can enter _silent mode_ by pressing `Ctrl+Alt+P`. To exit _silent mode_ you simply press `Ctrl+Alt+P` again.

#### Hotkeys

Both hotkeys can be changed in the settings (**General** tab): click into the hotkey box, press the key combination you want and click **Save**. The new hotkey works right away, no restart needed. **Clear** (or Backspace in the box) switches a hotkey off.

- **Silent mode hotkey** (default `Ctrl+Alt+P`): switches silent mode on and off.
- **Pause hotkey** (default: none): pauses Carnac, so no popups are shown, until you press it again.

A hotkey needs at least one of Ctrl, Alt or Shift plus one other key, and the two hotkeys must differ. Carnac does not swallow the hotkey: the application that has the focus still receives it, so pick a combination that application does not use. You can also edit `SilentModeHotkey` and `PauseHotkey` (for example `"Ctrl+Alt+P"`) in the settings file. While a process filter is set (Appearance tab), the hotkeys are only recognised when a matching application has the focus; the tray menu always works.

#### Tray icon menu

Hover over the Carnac icon in the notification area to see whether Carnac is active, paused or in silent mode. Right-click it for:

- **Settings...** (a left click opens the settings too)
- **Pause** / **Resume**: no popups are shown while Carnac is paused
- **Silent mode**: the same switch as the silent mode hotkey
- **Exit**

The menu shows the current hotkeys next to the items. Pause and silent mode are not remembered, Carnac always starts with both switched off.

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

#### Choosing which keys are shown

Everything you type is shown by default. *Preferences*, *Appearance* has four ways to show less:

- **Shortcuts Only** shows only the shortcuts listed in your keymaps.
- **Only keys with Modifiers** shows only key presses made with Ctrl, Alt or Windows, or with Shift when the key does not type a character (Enter, Tab, arrows, F-keys).
- **Keys to show** switches whole groups of keys off: letters, digits, punctuation, Space/Enter/Tab, editing keys (Backspace, Delete, Insert, Esc), navigation keys (arrows, Home, End, Page Up/Down), function keys and all other keys. Tick only *Function keys* and *Navigation* to see F5 or the up arrow but not the text you type. Key presses made with Ctrl, Alt or Windows, and shortcuts recognised from your keymaps, are always shown by these boxes. The two options above are applied in addition, so they can only hide more.
- **Ignored Keys** lists keys that are never shown, separated by commas or new lines and written like keymap keys, for example `W,A,S,D` or `Ctrl+Alt+Delete`. Modifiers must match, so `W` does not hide `Ctrl+W` or `Shift+W`. Ignored keys still count for the shortcuts in your keymaps (for a chord such as `Ctrl+K,S` the `S` can be ignored and the chord is still recognised). Entries Carnac cannot understand are skipped and reported to the Windows debug output.

#### Capturing the popups in OBS or XSplit

The popups are drawn by a transparent, click-through overlay window that is hidden from window lists, so the *Window Capture* source of OBS (and the window pickers of other tools) cannot select it by default. To capture it as a window:

1. Open the Preferences (click the tray icon), tick **Capture for OBS** on the *General* tab (it applies immediately) and press **Save** to keep it. The overlay now shows up in window lists; it stays click-through and never takes the keyboard focus. Untick it again to hide the overlay from window lists.
2. In OBS add a *Window Capture* source and select `[Carnac.exe]: Carnac Overlay`. The title of the overlay window is always `Carnac Overlay`.
3. Set *Capture Method* to *Windows 10 (1903 and up)*. If your version of OBS has an *Allow Transparency* option, enable it, otherwise the transparent area around the popups may be captured as black. (The option names are those of recent OBS versions and may differ slightly in yours.)
4. The overlay window is as wide as the popups (*Popup Text Width* plus the left and right offsets) and as high as the monitor it is on, so crop the source in the scene to the area you want.
5. The class name of a WPF window contains a new random id with every start of Carnac. If OBS does not find the window again after restarting Carnac, set *Window Match Priority* to *Window title must match*.

Things to know:

- **Recorded but not shown on your own screen** is not possible with the overlay window itself. The Windows call that hides a window from screen captures (`SetWindowDisplayAffinity`) does the opposite of what is needed: it removes the window from the capture. The practical way is to show the popups on a dedicated monitor or virtual display (choose it in the Preferences) and capture that display or the overlay window.
- **Windows Game Bar** (`Win+G`) only records the game window, not overlays of other applications, so the popups do not appear in Game Bar recordings. Use OBS or another tool that captures the desktop or a window.
- While **Capture for OBS** is on, other tools that list windows (for example the window picker of ShareX) list the overlay as well.

### Troubleshooting

#### Carnac stopped showing keys or closed by itself

Carnac writes a log file to `%APPDATA%\Carnac\logs`, one file per day: `carnac-YYYYMMDD.log` (paste that path into the Windows Explorer address bar). When errors keep happening, Carnac also shows a notice at the tray icon; click it to open the folder.

The log contains error messages with their stack trace, never the keys you type, but it can mention program names (for example when Carnac cannot read a program's icon). A log file is limited to 1 MB (the previous part is kept as `carnac-YYYYMMDD.old.log`), repeated identical entries are written once, and files older than 14 days are deleted automatically.

If you [open an issue](https://github.com/doggy8088/carnac/issues), please attach the newest entries of the log.

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

    The workflow builds and tests Carnac, compiles the installer, zips the portable copy and publishes `Carnac-2.4.1-Setup.exe` and `Carnac-2.4.1-portable.zip` with their SHA256 checksums (`sha256sums.txt`).
2. Submit the new version to [winget-pkgs](https://github.com/microsoft/winget-pkgs) with [wingetcreate](https://github.com/microsoft/winget-create):

    ```powershell
    wingetcreate update doggy8088.Carnac --version 2.4.1 --urls https://github.com/doggy8088/carnac/releases/download/v2.4.1/Carnac-2.4.1-Setup.exe --submit
    ```

To build the installer locally, build `src\Carnac.sln` in Release with the MSBuild flags from the workflow's Build step, then run `iscc /DAppVersion=2.4.1 installer\Carnac.iss`.

To build the portable zip locally, run `installer\New-PortableZip.ps1 -Version 2.4.1` after the same build. It packs exactly the files listed in the `[Files]` section of `installer\Carnac.iss` into `deploy\Carnac-2.4.1-portable.zip` and adds its hash to `deploy\sha256sums.txt`, and it fails when `Carnac.iss` uses something it does not understand. `installer\Test-PortableZip.ps1 -BuildDir src\Carnac\bin\Release` checks the script itself and the zip of the real build.

### Publish a new version to Chocolatey

The [`carnac`](https://community.chocolatey.org/packages/carnac) Chocolatey package lives in `src/Chocolatey`. It downloads the same `Carnac-<version>-Setup.exe` from GitHub Releases and installs it silently, so publish the GitHub release (and re-sign the installer, if you do that) first.

1. Point the package at the release and commit the change:

    ```powershell
    .\src\Chocolatey\Update-Package.ps1 -Version 2.4.1
    git commit -am "chore(chocolatey): 2.4.1"
    ```

    This updates the version, release notes link, installer URL and SHA256. Pushing it runs the [Chocolatey workflow](.github/workflows/chocolatey.yml), which packs the package, checks the checksum against the release asset and test-installs it.
2. Once the change is on `dev`, run the Chocolatey workflow from the Actions tab (or `gh workflow run chocolatey.yml --ref dev`) with **Push** enabled. It pushes the package with the `CHOCOLATEY_API_KEY` repository secret, which holds the API key from your [Chocolatey account page](https://community.chocolatey.org/account).
3. Watch moderation at `https://community.chocolatey.org/packages/carnac/<version>`. New versions go through automated validation and verification before they are approved.
