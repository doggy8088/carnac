<#
.SYNOPSIS
  Points the Chocolatey package at a published GitHub release of Carnac.

.DESCRIPTION
  Downloads sha256sums.txt from the doggy8088/carnac release for the given version,
  then updates carnac.nuspec (version, releaseNotes) and tools\chocolateyinstall.ps1
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

Write-Host "Reading $baseUrl/sha256sums.txt"
# GitHub serves release assets as application/octet-stream, so Content may be a byte array.
$response = Invoke-WebRequest -Uri "$baseUrl/sha256sums.txt" -UseBasicParsing
$sums = if ($response.Content -is [byte[]]) { [System.Text.Encoding]::UTF8.GetString($response.Content) } else { [string]$response.Content }
$pattern = '^([0-9A-Fa-f]{64})\s+' + [regex]::Escape($setupName) + '$'
$line = $sums -split "`r?`n" | Where-Object { $_ -match $pattern } | Select-Object -First 1
if (-not $line) { throw "sha256sums.txt for v$Version has no entry for $setupName" }
$checksum = ($line -split '\s+')[0].ToUpperInvariant()

$nuspecPath = Join-Path $chocoDir 'carnac.nuspec'
$nuspec = Get-Content $nuspecPath -Raw
$nuspec = $nuspec -replace '<version>[^<]*</version>', "<version>$Version</version>"
$nuspec = $nuspec -replace '<releaseNotes>[^<]*</releaseNotes>', "<releaseNotes>https://github.com/$Repository/releases/tag/v$Version</releaseNotes>"
Set-Content $nuspecPath $nuspec -NoNewline

$installPath = Join-Path $chocoDir 'tools\chocolateyinstall.ps1'
$install = Get-Content $installPath -Raw
$install = $install -replace "url\s*=\s*'[^']*'", "url            = '$baseUrl/$setupName'"
$install = $install -replace "checksum\s*=\s*'[^']*'", "checksum       = '$checksum'"
Set-Content $installPath $install -NoNewline

Write-Host "Updated carnac.nuspec and tools\chocolateyinstall.ps1 to $Version ($checksum)"
