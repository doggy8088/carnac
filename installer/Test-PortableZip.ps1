<#
.SYNOPSIS
Self-test for New-PortableZip.ps1. Exit code 0 only when every check passes.

.DESCRIPTION
Runs the packer against a fake build folder and fixture .iss files (no Carnac build needed) and checks
the zip contents, the sha256sums.txt format and that unsupported installer syntax fails loudly.
With -BuildDir it also packs a real Release build and compares the zip with the files Carnac needs,
listed independently of Carnac.iss (Carnac.exe, its config, LICENSE.md and every keymap in
src\Carnac.Logic\Keymaps), then extracts the zip and compares every file with the build output.

.PARAMETER BuildDir
Optional real build output (src\Carnac\bin\Release) for the extra checks.

.PARAMETER WorkDir
Where the scratch folder is created and removed again. Defaults to the temp folder.

.EXAMPLE
.\installer\Test-PortableZip.ps1
.\installer\Test-PortableZip.ps1 -BuildDir src\Carnac\bin\Release
#>
[CmdletBinding()]
param(
    [string]$BuildDir,
    [string]$WorkDir = [System.IO.Path]::GetTempPath()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$packer = Join-Path $PSScriptRoot 'New-PortableZip.ps1'
$failures = 0

function Check([bool]$condition, [string]$message)
{
    if ($condition) { Write-Host "  ok    $message" }
    else { Write-Host "  FAIL  $message"; $script:failures++ }
}

function Get-ZipEntries([string]$zipPath)
{
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try { return @($zip.Entries | ForEach-Object { $_.FullName }) }
    finally { $zip.Dispose() }
}

function Test-Throws([scriptblock]$action, [string]$expectedText)
{
    try { & $action | Out-Null; return $false }
    catch { return ($_.Exception.Message -like "*$expectedText*") }
}

function New-Files([string]$root, [string[]]$relativePaths)
{
    foreach ($relative in $relativePaths)
    {
        $path = Join-Path $root $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path) | Out-Null
        Set-Content -LiteralPath $path -Value "fake $relative" -Encoding ascii
    }
}

$scratch = Join-Path $WorkDir ('carnac-portable-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
try
{
    # ---- fake build folder: the app files plus things that must never be shipped ----
    $fakeBuild = Join-Path $scratch 'build'
    New-Files $fakeBuild @('Carnac.exe', 'Carnac.exe.config', 'Carnac.pdb', 'Carnac.vshost.exe', 'Carnac.Logic.dll',
        'Keymaps\one.yml', 'Keymaps\two.yml', 'Keymaps\notes.txt')
    $out = Join-Path $scratch 'out'
    $zipPath = Join-Path $out 'Carnac-1.2.3-portable.zip'

    Write-Host 'Zip built from the real Carnac.iss and a fake build folder'
    New-Item -ItemType Directory -Path $out | Out-Null
    Set-Content -LiteralPath (Join-Path $out 'sha256sums.txt') -Value ('A' * 64 + '  Carnac-1.2.3-Setup.exe') -Encoding ascii
    & $packer -Version 1.2.3 -BuildDir $fakeBuild -OutputDir $out 6>$null
    $entries = Get-ZipEntries $zipPath
    Check ($entries -contains 'Carnac.exe') 'Carnac.exe is at the zip root'
    Check ($entries -contains 'Carnac.exe.config') 'Carnac.exe.config is at the zip root'
    Check ($entries -contains 'LICENSE.md') 'LICENSE.md is at the zip root'
    Check (($entries -contains 'Keymaps/one.yml') -and ($entries -contains 'Keymaps/two.yml')) 'keymaps are in Keymaps/ with forward slashes'
    Check (-not ($entries | Where-Object { $_ -match '\\' })) 'no entry name contains a backslash'
    Check (-not ($entries | Where-Object { $_ -match '\.pdb$|vshost|\.dll$|\.txt$' })) 'pdb, vshost, dll and non-yml keymap files are left out'

    Write-Host 'sha256sums.txt'
    $lines = @(Get-Content -LiteralPath (Join-Path $out 'sha256sums.txt'))
    Check ($lines.Count -eq 2) 'the installer line is kept and one zip line is added'
    Check ($lines[0] -eq ('A' * 64 + '  Carnac-1.2.3-Setup.exe')) 'the installer line is unchanged'
    Check ($lines[1] -cmatch '^[0-9A-F]{64}  Carnac-1\.2\.3-portable\.zip$') 'zip line is "<UPPERCASE SHA256><2 spaces><file name>"'
    Check ($lines[1] -eq ((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash + '  Carnac-1.2.3-portable.zip')) 'the recorded hash is the hash of the zip'
    $raw = [System.IO.File]::ReadAllText((Join-Path $out 'sha256sums.txt'))
    Check ((-not $raw.Contains("`r")) -and $raw.EndsWith("`n")) 'LF line endings (so sha256sum -c works on Linux/macOS) and a final newline'
    & $packer -Version 1.2.3 -BuildDir $fakeBuild -OutputDir $out 6>$null
    $lines = @(Get-Content -LiteralPath (Join-Path $out 'sha256sums.txt'))
    Check ($lines.Count -eq 2) 'running again replaces the zip line instead of adding a second one'

    # ---- installer syntax the packer does not model must fail, not produce a wrong zip ----
    Write-Host 'Unsupported or broken input fails loudly'
    $iss = Join-Path $scratch 'fixture.iss'
    $header = "[Setup]`nAppName=Test`n[Icons]`nName: `"{autoprograms}\Carnac`"; Filename: `"{app}\Carnac.exe`"`n[Files]`n"
    Set-Content -LiteralPath $iss -Encoding ascii -Value ($header + '; a comment' + "`n" + 'Source: "{#BuildDir}\Carnac.exe"; DestDir: "{app}"; Flags: ignoreversion' + "`n" + '[Run]' + "`n" + 'Filename: "{app}\Carnac.exe"')
    $fixtureOut = Join-Path $scratch 'fixture-out'
    & $packer -Version 1.2.3 -BuildDir $fakeBuild -OutputDir $fixtureOut -IssPath $iss 6>$null
    Check ((Get-ZipEntries (Join-Path $fixtureOut 'Carnac-1.2.3-portable.zip')) -join ',' -eq 'Carnac.exe') 'lines outside [Files] and comments are ignored'

    $cases = @(
        @{ Text = 'Source: "{#BuildDir}\Carnac.exe"; DestDir: "{app}"; Excludes: "*.pdb"'; Expect = "Unsupported 'Excludes'"; Name = 'an Excludes parameter' },
        @{ Text = 'Source: "{#BuildDir}\Carnac.exe"; DestDir: "{app}"; Check: IsAdmin'; Expect = "Unsupported 'Check'"; Name = 'a Check parameter' },
        @{ Text = 'Source: "{#BuildDir}\Keymaps\*"; DestDir: "{app}"; Flags: recursesubdirs'; Expect = "Unsupported flag 'recursesubdirs'"; Name = 'recursesubdirs' },
        @{ Text = 'Source: "{#BuildDir}\Carnac.exe"; DestDir: "{pf}\Carnac"'; Expect = 'Unsupported DestDir'; Name = 'a DestDir outside {app}' },
        @{ Text = 'Source: "{tmp}\Carnac.exe"; DestDir: "{app}"'; Expect = 'Unsupported constant'; Name = 'an unknown constant in Source' },
        @{ Text = 'Source: "{#BuildDir}\missing.exe"; DestDir: "{app}"'; Expect = 'matched no file'; Name = 'a Source that matches nothing' },
        @{ Text = 'Source: "{#BuildDir}\Keymaps\*.xyz"; DestDir: "{app}\Keymaps"'; Expect = 'matched no file'; Name = 'a wildcard that matches nothing' },
        @{ Text = 'Source: "{#BuildDir}\Carnac.exe"'; Expect = 'Cannot read Source/DestDir'; Name = 'a line without DestDir' },
        @{ Text = "Source: `"{#BuildDir}\Carnac.exe`"; DestDir: `"{app}`"`nSource: `"{#BuildDir}\Carnac.exe`"; DestDir: `"{app}`""; Expect = 'more than one file'; Name = 'two entries with the same target' }
    )
    foreach ($case in $cases)
    {
        Set-Content -LiteralPath $iss -Encoding ascii -Value ($header + $case.Text)
        Check (Test-Throws { & $packer -Version 1.2.3 -BuildDir $fakeBuild -OutputDir $fixtureOut -IssPath $iss } $case.Expect) "$($case.Name) is rejected"
    }
    Check (Test-Throws { & $packer -Version 2.4 -BuildDir $fakeBuild -OutputDir $fixtureOut } 'must look like') 'a version that is not x.y.z is rejected'
    Check (Test-Throws { & $packer -Version 1.2.3 -BuildDir (Join-Path $scratch 'nope') -OutputDir $fixtureOut } 'not found') 'a missing build folder is rejected'

    # ---- optional: the real build, checked against a list that does not come from Carnac.iss ----
    if ($BuildDir)
    {
        Write-Host 'Real build'
        $BuildDir = (Resolve-Path -LiteralPath $BuildDir).Path
        $repo = Split-Path -Parent $PSScriptRoot
        $realOut = Join-Path $scratch 'real-out'
        & $packer -Version 9.9.9 -BuildDir $BuildDir -OutputDir $realOut 6>$null
        $realZip = Join-Path $realOut 'Carnac-9.9.9-portable.zip'
        $expected = @('Carnac.exe', 'Carnac.exe.config', 'LICENSE.md') +
            @(Get-ChildItem -LiteralPath (Join-Path $repo 'src\Carnac.Logic\Keymaps') -Filter '*.yml' | ForEach-Object { 'Keymaps/' + $_.Name })
        $actual = Get-ZipEntries $realZip
        Check (-not (Compare-Object $expected $actual -CaseSensitive)) "zip holds exactly Carnac.exe, its config, LICENSE.md and the $($expected.Count - 3) keymaps of src\Carnac.Logic\Keymaps"

        $extracted = Join-Path $scratch 'extracted'
        [System.IO.Compression.ZipFile]::ExtractToDirectory($realZip, $extracted)
        $sameFiles = $true
        foreach ($entry in $actual)
        {
            $source = if ($entry -eq 'LICENSE.md') { Join-Path $repo 'LICENSE.md' } else { Join-Path $BuildDir $entry }
            $copy = Join-Path $extracted $entry
            if (-not ((Test-Path -LiteralPath $copy) -and ((Get-FileHash -LiteralPath $copy).Hash -eq (Get-FileHash -LiteralPath $source).Hash))) { $sameFiles = $false; Write-Host "        differs: $entry" }
        }
        Check $sameFiles 'the extracted files are byte-identical to the build output'
    }
}
finally
{
    Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures -gt 0) { Write-Host "PORTABLE ZIP TESTS: FAILED ($failures)"; exit 1 }
Write-Host 'PORTABLE ZIP TESTS: OK'
exit 0
