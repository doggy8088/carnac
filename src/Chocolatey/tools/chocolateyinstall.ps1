$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  fileType       = 'exe'
  url            = 'https://github.com/doggy8088/carnac/releases/download/v2.4.0/Carnac-2.4.0-Setup.exe'
  checksum       = '55866C6E90BA615E3E2CA82ACA06994F0FB9A53552BED18490D1E12DBDC57A4D'
  checksumType   = 'sha256'
  # Inno Setup silent install. The installer is per user (PrivilegesRequired=lowest),
  # so it lands in %LocalAppData%\Programs\Carnac for the account running Chocolatey.
  silentArgs     = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LOG=`"$env:TEMP\$env:ChocolateyPackageName.$env:ChocolateyPackageVersion.Install.log`""
  validExitCodes = @(0)
  softwareName   = 'Carnac'
}

# Versions 2.3.x of this package installed Carnac with Squirrel into %LocalAppData%\carnac.
# Chocolatey does not run the old package's uninstall script on upgrade, so remove that
# copy here. Otherwise two Carnacs would start at sign-in.
$squirrelUpdater = Join-Path $env:LOCALAPPDATA 'carnac\Update.exe'
if (Test-Path $squirrelUpdater) {
  $squirrelDir = Split-Path -Parent $squirrelUpdater
  Write-Host "Removing the Squirrel-based Carnac 2.3.x install from $squirrelDir"
  Get-Process -Name 'Carnac' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($squirrelDir, [System.StringComparison]::OrdinalIgnoreCase) } |
    Stop-Process -Force -ErrorAction SilentlyContinue
  $squirrel = Start-Process -FilePath $squirrelUpdater -ArgumentList '--uninstall' -Wait -PassThru -WindowStyle Hidden
  if ($squirrel.ExitCode -ne 0) {
    Write-Warning "Squirrel uninstall exited with $($squirrel.ExitCode). Removing $squirrelDir anyway."
  }
  Remove-Item $squirrelDir -Recurse -Force -ErrorAction SilentlyContinue
}

Install-ChocolateyPackage @packageArgs
