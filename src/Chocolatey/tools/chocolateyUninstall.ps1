$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  softwareName   = 'Carnac'
  fileType       = 'exe'
  silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
  validExitCodes = @(0)
}

# The Inno Setup installer is per user, so the uninstall entry lives under HKCU with a stable AppId.
$innoAppId = '{DB5C24A2-1558-4272-8C3C-676D68A1E405}_is1'
$innoRegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$innoAppId"

$uninstallFile = $null
if (Test-Path $innoRegPath) {
  $innoKey = Get-ItemProperty -Path $innoRegPath -ErrorAction SilentlyContinue
  if ($innoKey -and $innoKey.UninstallString) {
    $uninstallFile = "$($innoKey.UninstallString)".Trim('"')
  }
}

if (-not $uninstallFile) {
  # Fallback to Get-UninstallRegistryKey across hives if not found in HKCU
  [array]$keys = Get-UninstallRegistryKey -SoftwareName $packageArgs['softwareName']
  $matchedKey = $keys | Where-Object { $_.PSChildName -eq $innoAppId } | Select-Object -First 1
  if (-not $matchedKey -and $keys.Count -eq 1) {
    $matchedKey = $keys[0]
  }

  if ($matchedKey) {
    $uninstallFile = "$($matchedKey.UninstallString)".Trim('"')
  } elseif ($keys.Count -gt 1) {
    Write-Warning "$($keys.Count) matches found for $($packageArgs['softwareName']), but none matched Inno AppId $innoAppId."
    Write-Warning 'To prevent accidental data loss, no programs will be uninstalled.'
    Write-Warning 'Please alert the package maintainer the following keys were matched:'
    $keys | ForEach-Object { Write-Warning "- $($_.DisplayName) ($($_.UninstallString))" }
  }
}

if ($uninstallFile) {
  $packageArgs['file'] = $uninstallFile
  Uninstall-ChocolateyPackage @packageArgs
} elseif (-not (Test-Path $innoRegPath) -and ($keys -eq $null -or $keys.Count -eq 0)) {
  Write-Warning "$($packageArgs['packageName']) has already been uninstalled by other means."
}
