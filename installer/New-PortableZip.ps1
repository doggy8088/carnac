<#
.SYNOPSIS
Builds the portable Carnac zip, Carnac-<version>-portable.zip, from the files the installer copies.

.DESCRIPTION
The file list is read from the [Files] section of Carnac.iss, so the zip cannot drift from the
installer: whatever the installer puts in {app} is at the root of the zip with the same relative
path (Carnac.exe, Carnac.exe.config, Keymaps/*.yml, LICENSE.md). Costura embeds every dependency
into Carnac.exe, so nothing else is needed to run it.

The script fails, instead of guessing, when Carnac.iss uses a [Files] feature it does not model
(other parameters than Source/DestDir/Flags, a DestDir outside {app}, recursion, ...), when a
Source matches no file, and when the finished zip does not contain exactly the expected entries.

The SHA256 of the zip is added to sha256sums.txt in the output folder (created if missing; an
existing line for the same zip is replaced), in the same "<HASH>  <file>" format the installer
step writes (rewritten with LF line endings so sha256sum -c works everywhere), so one sha256sums.txt
covers both release assets.

Zip entries are written with '/' separators on every PowerShell edition (Windows PowerShell 5.1
Compress-Archive writes '\', which other unzip tools mishandle).

.PARAMETER Version
Release version without the leading v, for example 2.4.0.

.PARAMETER BuildDir
Release build output of src\Carnac. Defaults to ..\src\Carnac\bin\Release, like Carnac.iss.

.PARAMETER OutputDir
Folder for the zip and sha256sums.txt. Defaults to ..\deploy, like Carnac.iss.

.PARAMETER IssPath
The Inno Setup script that defines the file list. Defaults to Carnac.iss next to this script.

.EXAMPLE
.\installer\New-PortableZip.ps1 -Version 2.4.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$BuildDir = (Join-Path $PSScriptRoot '..\src\Carnac\bin\Release'),
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\deploy'),
    [string]$IssPath = (Join-Path $PSScriptRoot 'Carnac.iss')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

# [Files] flags that change which files the installer copies; this script does not model them.
$unsupportedFlags = 'recursesubdirs', 'createallsubdirs', 'external', 'skipifsourcedoesntexist',
    'dontcopy', 'onlyifdestfileexists', 'onlyifdoesntexist', 'deleteafterinstall'

function Get-InstallerFiles([string]$IssPath, [string]$BuildDir)
{
    $issDir = Split-Path -Parent $IssPath
    $section = ''
    foreach ($raw in Get-Content -LiteralPath $IssPath)
    {
        $line = $raw.Trim()
        if ($line -match '^\[(\w+)\]$') { $section = $Matches[1]; continue }
        if ($section -ne 'Files' -or $line -eq '' -or $line.StartsWith(';')) { continue }

        $params = @{}
        foreach ($m in [regex]::Matches($line, '(?<name>\w+)\s*:\s*(?:"(?<value>[^"]*)"|(?<value>[^;]*))\s*(?:;|$)'))
        {
            $params[$m.Groups['name'].Value] = $m.Groups['value'].Value.Trim()
        }
        foreach ($name in $params.Keys)
        {
            if ('Source', 'DestDir', 'Flags' -notcontains $name)
            {
                throw "Unsupported '$name' parameter in [Files] of $IssPath ('$line'). Teach New-PortableZip.ps1 about it first."
            }
        }
        if (-not $params.ContainsKey('Source') -or -not $params.ContainsKey('DestDir'))
        {
            throw "Cannot read Source/DestDir from [Files] line of ${IssPath}: '$line'"
        }
        if ($params.ContainsKey('Flags'))
        {
            foreach ($flag in $params['Flags'] -split '\s+')
            {
                if ($unsupportedFlags -contains $flag) { throw "Unsupported flag '$flag' in [Files] of $IssPath ('$line'). Teach New-PortableZip.ps1 about it first." }
            }
        }

        # DestDir must be {app} or a folder below it; the zip root plays the role of {app}.
        if ($params['DestDir'] -notmatch '^\{app\}(\\(?<sub>[^{}]+?))?\\?$')
        {
            throw "Unsupported DestDir '$($params['DestDir'])' in [Files] of $IssPath ('$line'): only {app} and folders below it can be packed."
        }
        $sub = ''
        if ($Matches['sub']) { $sub = $Matches['sub'] -replace '\\', '/' }

        # Source: {#BuildDir} is the only preprocessor constant, other paths are relative to the .iss.
        $source = $params['Source'].Replace('{#BuildDir}', $BuildDir)
        if ($source -match '[{}]') { throw "Unsupported constant in Source '$($params['Source'])' in [Files] of $IssPath." }
        if (-not [System.IO.Path]::IsPathRooted($source)) { $source = Join-Path $issDir $source }
        # Normalise only the folder: Windows PowerShell 5.1 (.NET Framework) rejects '*' in GetFullPath.
        $sourceDir = [System.IO.Path]::GetFullPath((Split-Path -Parent $source))
        $pattern = Split-Path -Leaf $source

        $files = @()
        if (Test-Path -LiteralPath $sourceDir -PathType Container)
        {
            $files = @(Get-ChildItem -LiteralPath $sourceDir -File | Where-Object { $_.Name -like $pattern } | Sort-Object Name)
        }
        if ($files.Count -eq 0) { throw "[Files] Source '$($params['Source'])' of $IssPath matched no file (looked in '$sourceDir')." }

        foreach ($file in $files)
        {
            $entry = $file.Name
            if ($sub) { $entry = "$sub/$($file.Name)" }
            [pscustomobject]@{ Entry = $entry; Path = $file.FullName }
        }
    }
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version '$Version' must look like 2.4.0" }
if (-not (Test-Path -LiteralPath $IssPath -PathType Leaf)) { throw "Installer script '$IssPath' not found." }
if (-not (Test-Path -LiteralPath $BuildDir -PathType Container)) { throw "Build output '$BuildDir' not found. Build src\Carnac.sln in Release first." }
$IssPath = (Resolve-Path -LiteralPath $IssPath).Path
$BuildDir = (Resolve-Path -LiteralPath $BuildDir).Path

$entries = @(Get-InstallerFiles $IssPath $BuildDir)
$duplicates = @($entries | Group-Object Entry | Where-Object { $_.Count -gt 1 })
if ($duplicates.Count -gt 0) { throw "Carnac.iss installs more than one file to '$($duplicates[0].Name)'." }

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$OutputDir = (Resolve-Path -LiteralPath $OutputDir).Path
$zipName = "Carnac-$Version-portable.zip"
$zipPath = Join-Path $OutputDir $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try
{
    foreach ($e in $entries)
    {
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $e.Path, $e.Entry, [System.IO.Compression.CompressionLevel]::Optimal)
    }
}
finally
{
    $zip.Dispose()
}

# Read the finished zip back: it must hold exactly the files the installer copies, nothing else.
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try { $actual = @($zip.Entries | ForEach-Object { $_.FullName }) }
finally { $zip.Dispose() }
$expected = @($entries | ForEach-Object { $_.Entry })
if (Compare-Object $expected $actual -CaseSensitive) { throw "$zipName holds [$($actual -join ', ')] but Carnac.iss installs [$($expected -join ', ')]." }

$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
$sumsPath = Join-Path $OutputDir 'sha256sums.txt'
$lines = @()
if (Test-Path -LiteralPath $sumsPath)
{
    $lines = @(Get-Content -LiteralPath $sumsPath | Where-Object { $_.Trim() -ne '' -and -not $_.EndsWith("  $zipName") })
}
$lines += "$hash  $zipName"
# LF line endings, so 'sha256sum -c sha256sums.txt' also works on Linux, macOS and WSL (with CRLF it
# looks for files whose names end in a carriage return). This also normalises the installer's line.
[System.IO.File]::WriteAllText($sumsPath, (($lines -join "`n") + "`n"), [System.Text.Encoding]::ASCII)

Write-Host "Created $zipPath"
foreach ($name in $actual) { Write-Host "  $name" }
Write-Host "Added to ${sumsPath}: $hash  $zipName"
