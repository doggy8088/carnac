[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$RunTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

Push-Location $repoRoot
try {
    # 1. Stop any running Carnac instance so the executable is not locked during build
    $running = Get-Process -Name Carnac -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "Stopping running Carnac process (PID: $($running.Id -join ', '))..." -ForegroundColor Yellow
        $running | Stop-Process -Force
        Start-Sleep -Milliseconds 300
    }

    # 2. Locate MSBuild.exe
    $msbuildCmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($msbuildCmd) {
        $msbuild = $msbuildCmd.Source
    } else {
        $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
        if (-not (Test-Path $vswhere)) {
            throw "Cannot find vswhere.exe at '$vswhere' and msbuild.exe is not in PATH."
        }
        $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if (-not $msbuild) {
            throw "MSBuild.exe could not be found via vswhere."
        }
    }

    # 3. Locate .NET Framework 4.5 Reference Assemblies
    $refPathCandidates = @(
        (Join-Path $repoRoot "build-tools\Microsoft.NETFramework.ReferenceAssemblies.net45.1.0.3\build"),
        (Join-Path $env:USERPROFILE ".nuget\packages\microsoft.netframework.referenceassemblies.net45\1.0.3\build")
    )
    $refPath = $refPathCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $refPath) {
        Write-Host "Installing Microsoft.NETFramework.ReferenceAssemblies.net45..." -ForegroundColor Cyan
        & nuget install Microsoft.NETFramework.ReferenceAssemblies.net45 -Version 1.0.3 -OutputDirectory (Join-Path $repoRoot "build-tools") -NonInteractive
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install Microsoft.NETFramework.ReferenceAssemblies.net45 via nuget."
        }
        $refPath = Join-Path $repoRoot "build-tools\Microsoft.NETFramework.ReferenceAssemblies.net45.1.0.3\build"
    }

    # 4. Build solution
    Write-Host "Building Carnac ($Configuration)..." -ForegroundColor Cyan
    & $msbuild (Join-Path $repoRoot "src\Carnac.sln") `
        -m `
        -v:minimal `
        "-p:Configuration=$Configuration" `
        "-p:TargetFrameworkRootPath=$refPath" `
        -p:GenerateManifests=false `
        -p:SignManifests=false

    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with exit code $LASTEXITCODE."
    }

    # 5. Optionally run unit tests
    if ($RunTests) {
        $xunit = Get-ChildItem -Path (Join-Path $repoRoot "build-tools"), (Join-Path $env:USERPROFILE ".nuget\packages") `
            -Recurse -Filter "xunit.console.clr4.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $xunit) {
            & nuget install xunit.runners -Version 1.9.2 -OutputDirectory (Join-Path $repoRoot "build-tools") -NonInteractive
            $xunit = Get-ChildItem -Path (Join-Path $repoRoot "build-tools") -Recurse -Filter "xunit.console.clr4.exe" | Select-Object -First 1
        }
        $testDll = Join-Path $repoRoot "src\Carnac.Tests\bin\$Configuration\Carnac.Tests.dll"
        Write-Host "Running unit tests..." -ForegroundColor Cyan
        & $xunit.FullName $testDll /silent
        if ($LASTEXITCODE -ne 0) {
            throw "Unit tests failed with exit code $LASTEXITCODE."
        }
    }

    # 6. Launch Carnac
    $exePath = Join-Path $repoRoot "src\Carnac\bin\$Configuration\Carnac.exe"
    if (-not (Test-Path $exePath)) {
        throw "Built executable not found at '$exePath'."
    }

    Write-Host "Starting $exePath..." -ForegroundColor Green
    Start-Process -FilePath $exePath -WorkingDirectory (Split-Path $exePath -Parent)
}
finally {
    Pop-Location
}
