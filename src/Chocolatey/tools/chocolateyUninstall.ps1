$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  softwareName   = 'Carnac'
  fileType       = 'exe'
  silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
  validExitCodes = @(0)
}

# The Inno Setup installer is per user, so the uninstall entry lives under HKCU.
# Get-UninstallRegistryKey searches both HKLM and HKCU.
[array]$keys = Get-UninstallRegistryKey -SoftwareName $packageArgs['softwareName']

if ($keys.Count -eq 1) {
  $keys | ForEach-Object {
    $packageArgs['file'] = "$($_.UninstallString)".Trim('"')
    Uninstall-ChocolateyPackage @packageArgs
  }
} elseif ($keys.Count -eq 0) {
  Write-Warning "$($packageArgs['packageName']) has already been uninstalled by other means."
} else {
  Write-Warning "$($keys.Count) matches found for $($packageArgs['softwareName'])."
  Write-Warning 'To prevent accidental data loss, no programs will be uninstalled.'
  Write-Warning 'Please alert the package maintainer the following keys were matched:'
  $keys | ForEach-Object { Write-Warning "- $($_.DisplayName) ($($_.UninstallString))" }
}
