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

    The workflow builds and tests Carnac, compiles the installer and publishes `Carnac-2.4.1-Setup.exe` with its SHA256 checksum.
2. Submit the new version to [winget-pkgs](https://github.com/microsoft/winget-pkgs) with [wingetcreate](https://github.com/microsoft/winget-create):

    ```powershell
    wingetcreate update doggy8088.Carnac --version 2.4.1 --urls https://github.com/doggy8088/carnac/releases/download/v2.4.1/Carnac-2.4.1-Setup.exe --submit
    ```

To build the installer locally, build `src\Carnac.sln` in Release with the MSBuild flags from the workflow's Build step, then run `iscc /DAppVersion=2.4.1 installer\Carnac.iss`.
