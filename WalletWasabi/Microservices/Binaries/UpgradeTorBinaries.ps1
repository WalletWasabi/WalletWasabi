# Script downloads Tor Browsers, then extract the downloaded archives and copies Tor binaries to "temp/<version/Tor/<os-platform>" folders to be consumed by Wasabi Wallet.

# # Examples
# 1] `.\UpgradeTorBinaries.ps1` runs the script which downloads Tor Browser binaries, extract them to "temp" (where the script is placed) and copies Tor binaries to "temp/<tor-browser-version>/Tor/<os-platform>".
#   Then the Tor binaries used by Wasabi Wallet are replaced with the just extracted ones.
# 2] `.\UpgradeTorBinaries.ps1 -Debug` to show debug information what the script does.
# 3] `.\UpgradeTorBinaries.ps1 -skipDownloading` skips downloading Tor Browser binaries and continues as in 1]. Useful for script testing.
#
# # Notes
#
# Before running the script, you need to install 7zip
# $ winget install --id 7zip.7zip
# $ brew install sevenzip
# $ apt install p7zip-full

[CmdletBinding()]
param(
  [Parameter(Mandatory=$false)][Switch]$skipDownloading,
  [Parameter(Mandatory=$false)][Switch]$skipExtractingBrowserArchives,
  [Parameter(Mandatory=$false)][Switch]$skipExtractingTorBinaries,
  [Parameter(Mandatory=$false)][Switch]$skipReplacingTorBinaries)

Set-StrictMode -Version 3
$ErrorActionPreference = "Stop"

# <Settings>
$version = "11.0.14"
$distUri = "https://www.torproject.org/dist/torbrowser/${version}"

$supportedPlatforms = @(
  "win64",
  "osx64",
  "lin64"
)
# </Settings>

if ($IsWindows) {
  $sevenZip = 'C:\Program Files\7-Zip\7z.exe'
} else {
  $sevenZip = '7z'
}

$windowsInstaller = "torbrowser-install-win64-${version}_en-US.exe"
$macDmg = "TorBrowser-${version}-osx64_en-US.dmg"
$linuxTarball = "tor-browser-linux64-${version}_en-US.tar"
$linuxCompressedTarball = "$linuxTarball.xz"

$packages = @(
  $windowsInstaller,
  $macDmg,
  $linuxCompressedTarball
)

# Remember old working directory to restore it later.
$prevPwd = $PWD;
Set-Location -LiteralPath $PSScriptRoot | out-null
Write-Output "# Set PWD to '$PSScriptRoot'."

try {
  if (!$skipDownloading) {
    # Start with clean "temp/$version" folder.
    Remove-Item "temp/$version" -Force -Recurse -ErrorAction SilentlyContinue
    mkdir -p "temp/$version"
    Set-Location -LiteralPath "temp/$version" | out-null

    Write-Output "# Download Tor Browsers for supported platforms."
    foreach ($fileName in $packages) {
      $uri = "$distUri/$fileName"
      Write-Output "# Downloading ${uri} ..."
      Invoke-WebRequest -Uri $uri -out "$fileName"
    }
  } else {
    if (!(Test-Path -Path "temp/$version")) {
      Write-Error "Folder 'temp/$version' does not exist. Run the script again without -skipDownloading switch."
      exit
    }

    Set-Location -LiteralPath "temp/$version" | out-null
    Write-Output "# Skip downloading Tor Browsers."
  }

  if ($skipExtractingBrowserArchives) {
    Write-Output "# Skip extracting Tor Browser archives."
  } else {
    Remove-Item "TorBrowser/*" -Force -Recurse -ErrorAction SilentlyContinue

    Write-Output "# Extract Tor Browser for Windows."
    & $sevenZip "x" "$macDmg" "-oTorBrowser/macOS" # Extract using 7zip to an output directory.

    Write-Output "# Extract Tor Browser for macOS."
    & $sevenZip "x" "$windowsInstaller" "-oTorBrowser/Windows"

    Write-Output "# Extract Tor Browser for linux."
    # Shorter variant that does not work for me. See https://superuser.com/questions/80019/how-can-i-unzip-a-tar-gz-in-one-step-using-7-zip.
    # & $sevenZip "x" "-so" "$linuxCompressedTarball" | & $sevenZip "x" "-aoa" "-si" "-ttar" "-oTorBrowser/linux"   # -so ~ write to stdout, -si ~ read from stdin
    & $sevenZip "x" "$linuxCompressedTarball" "-y"
    & $sevenZip "x" "$linuxTarball" "-oTorBrowser/linux"
  }

  if ($skipExtractingTorBinaries) {
    Write-Output "# Skip extracting Tor binaries."
  } else {
    Write-Output "# Make sure we start with clean slate."
    Remove-Item -Recurse -Force "Tor/*" -ErrorAction SilentlyContinue

    Write-Output "# Extract Tor binary for Windows."
    mkdir "Tor/win64/"
    Copy-item -Recurse -Force -Path "TorBrowser/Windows/Browser/TorBrowser/Tor/*" -Destination "Tor/win64/" -Exclude "PluggableTransports"

    Write-Output "# Extract Tor binary for macOs."
    mkdir "Tor/osx64/"
    Copy-item -Recurse -Force -Path "TorBrowser/macOS/Tor Browser.app/Contents/MacOS/Tor/*" -Destination "Tor/osx64/" -Exclude "PluggableTransports"

    Write-Output "# Extract Tor binary for linux."
    mkdir "Tor/lin64/"
    Copy-item -Recurse -Force -Path "TorBrowser/linux/tor-browser_en-US/Browser/TorBrowser/Tor/*" -Destination "Tor/lin64/" -Exclude "PluggableTransports"
  }

  if ($skipReplacingTorBinaries) {
    Write-Output "# Skip replacing Tor binaries."
  } else {
    foreach ($platform in $supportedPlatforms) {
      Write-Output "# Replace Tor binaries in folder '$PSScriptRoot/$platform/Tor/'."
      Remove-Item -Recurse -Force "$PSScriptRoot/$platform/Tor/*" -Exclude "LICENSE" -ErrorAction SilentlyContinue
      Copy-item -Recurse -Force -Path "Tor/$platform/*" -Destination "$PSScriptRoot/$platform/Tor/"
    }
  }

  Write-Output "# Done."
}
finally {
    # Restore the previous location.
    Set-Location -Path $prevPwd | out-null
}
