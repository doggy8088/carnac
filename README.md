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

We use `Squirrel.Windows` to update your `carnac` application.

The application will check for updates in the background, if a new version has been released, it will automatically install the new version and once you restart `carnac` you will be up-to-date.

### Usage

#### Enabling silent mode

If you want to stop `Carnac` from recording certain key strokes, you can enter _silent mode_ by pressing `Ctrl+Alt+P`. To exit _silent mode_ you simply press `Ctrl+Alt+P` again.

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
