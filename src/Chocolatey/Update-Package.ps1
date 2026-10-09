<#
.SYNOPSIS
  Points the Chocolatey package at a published GitHub release of Carnac.

.DESCRIPTION
  Downloads the installer executable Carnac-<Version>-Setup.exe from the
  doggy8088/carnac release, computes its SHA256 checksum, then updates
  carnac.nuspec (version, releaseNotes) and tools\chocolateyinstall.ps1
  (installer URL and SHA256). Commit the result and run the Chocolatey workflow to
  pack, test and push it.

.EXAMPLE
  .\src\Chocolatey\Update-Package.ps1 -Version 2.4.1
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [ValidatePattern('^\d+\.\d+\.\d+$')]
  [string]$Version,

  [string]$Repository = 'doggy8088/carnac'
)

$ErrorActionPreference = 'Stop'
$chocoDir = $PSScriptRoot
$setupName = "Carnac-$Version-Setup.exe"
$baseUrl = "https://github.com/$Repository/releases/download/v$Version"
$setupUrl = "$baseUrl/$setupName"

Write-Host "Downloading $setupUrl to compute SHA256 checksum..."
$tempFile = [System.IO.Path]::GetTempFileName()
try {
  Invoke-WebRequest -Uri $setupUrl -OutFile $tempFile -UseBasicParsing
  $checksum = (Get-FileHash -Path $tempFile -Algorithm SHA256).Hash.ToUpperInvariant()
} finally {
  Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
}

$nuspecPath = Join-Path $chocoDir 'carnac.nuspec'
$nuspec = Get-Content $nuspecPath -Raw
$nuspec = $nuspec -replace '<version>[^<]*</version>', "<version>$Version</version>"
$nuspec = $nuspec -replace '<releaseNotes>[^<]*</releaseNotes>', "<releaseNotes>https://github.com/$Repository/releases/tag/v$Version</releaseNotes>"
Set-Content $nuspecPath $nuspec -Encoding UTF8 -NoNewline

$installPath = Join-Path $chocoDir 'tools\chocolateyinstall.ps1'
$install = Get-Content $installPath -Raw
$install = $install -replace "url\s*=\s*'[^']*'", "url            = '$baseUrl/$setupName'"
$install = $install -replace "checksum\s*=\s*'[^']*'", "checksum       = '$checksum'"
Set-Content $installPath $install -Encoding UTF8 -NoNewline

Write-Host "Updated carnac.nuspec and tools\chocolateyinstall.ps1 to $Version ($checksum)"
