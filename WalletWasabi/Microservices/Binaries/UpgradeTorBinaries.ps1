<#
.SYNOPSIS
    Downloads, extracts, and upgrades Tor Browser binaries for use in Wasabi Wallet.

.DESCRIPTION
    This script automates the process of obtaining the latest (or specified) Tor Browser binaries,
    extracting the Tor binaries from the platform-specific archives, and placing them into the
    correct folder structure expected by Wasabi Wallet (temp/<version>/Tor/<os-platform>).

    It can also replace the Tor binaries currently used by Wasabi Wallet with the newly extracted ones.

    Requires PowerShell 7+ and 7-Zip installed on the system.

.PARAMETER Version
    The Tor Browser version to download (e.g. "15.0.4"). This is a required parameter.

.PARAMETER Debug
    Switch that enables verbose/debug output to show detailed information about what the script is doing.

.PARAMETER SkipDownloading
    Switch that skips the step of downloading Tor Browser binaries. Useful for testing or when archives are already present in the working directory.

.EXAMPLE
    .\UpgradeTorBinaries.ps1 -Version "15.0.4"

    Downloads Tor Browser 15.0.4 for all supported platforms, extracts the archives,
    organizes binaries into temp/15.0.4/Tor/<platform>, and replaces Wasabi's current Tor binaries.

.EXAMPLE
    .\UpgradeTorBinaries.ps1 -Version "15.0.4" -Debug

    Same as above, but with detailed debug/verbose output shown in the console.

.EXAMPLE
    .\UpgradeTorBinaries.ps1 -Version "15.0.4" -SkipDownloading

    Skips downloading (assumes files are already present), extracts & copies binaries,
    and performs the Wasabi Wallet Tor upgrade.

.NOTES
    Prerequisites:
    - PowerShell 7 or newer
    - 7-Zip installed and available in PATH
      → Windows:   winget install --id 7zip.7zip
      → macOS:     brew install sevenzip
      → Linux:     apt install 7zip

    The script expects to be run from the directory where the "temp" folder should be created/used.

    Typically used by Wasabi Wallet developers/maintainers when a new Tor release should be integrated.

.LINK
    https://www.torproject.org/download/
    Tor Browser download page
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)]$version,
  [Parameter(Mandatory=$false)][Switch]$skipDownloading,
  [Parameter(Mandatory=$false)][Switch]$skipExtractingBrowserArchives,
  [Parameter(Mandatory=$false)][Switch]$skipExtractingTorBinaries,
  [Parameter(Mandatory=$false)][Switch]$skipReplacingTorBinaries,
  [Parameter(Mandatory=$false)][Switch]$skipReplacingGeoIpFiles)

Set-StrictMode -Version 3
$ErrorActionPreference = "Stop"

# <Settings>
$distUri = "https://www.torproject.org/dist/torbrowser/${version}"

$supportedPlatforms = @(
  "win64",
  "osx64",
  "lin64"
  "linux_arm64"
)
# </Settings>

if ($IsWindows) {
  $sevenZip = 'C:\Program Files\7-Zip\7z.exe'
} else {
  $sevenZip = '7zz'
}

$windowsInstaller = "tor-browser-windows-x86_64-portable-${version}.exe"
$macDmg = "tor-browser-macos-${version}.dmg"
$linuxTarball_arm64 = "tor-browser-linux-aarch64-${version}.tar"
$linuxCompressedTarball_arm64 = "$linuxTarball_arm64.xz"
$linuxTarball_x86_64 = "tor-browser-linux-x86_64-${version}.tar"
$linuxCompressedTarball_x86_64 = "$linuxTarball_x86_64.xz"

$packages = @(
  $windowsInstaller,
  $macDmg,
  $linuxCompressedTarball_arm64
  $linuxCompressedTarball_x86_64
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

    Write-Output "# Extract Tor Browser for linux (x86_64)."
    & $sevenZip "x" "$linuxCompressedTarball_x86_64" "-y"
    & $sevenZip "x" "$linuxTarball_x86_64" "-oTorBrowser/linux_x86_64"

    Write-Output "# Extract Tor Browser for linux (aarch64)."
    & $sevenZip "x" "$linuxCompressedTarball_arm64" "-y"
    & $sevenZip "x" "$linuxTarball_arm64" "-oTorBrowser/linux_arm64"
  }

  if ($skipExtractingTorBinaries) {
    Write-Output "# Skip extracting Tor binaries."
  } else {
    Write-Output "# Make sure we start with clean slate."
    Remove-Item -Recurse -Force "Tor/*" -ErrorAction SilentlyContinue

    Write-Output "# Extract Tor binary for Windows."
    mkdir -p "Tor/win64/"
    Copy-item -Recurse -Force -Path "TorBrowser/Windows/Browser/TorBrowser/Tor/*" -Destination "Tor/win64/" -Exclude "PluggableTransports"

    Write-Output "# Extract Tor binary for macOs."
    mkdir -p "Tor/osx64/"
    Copy-item -Recurse -Force -Path "TorBrowser/macOS/Tor Browser Alpha/Tor Browser Alpha.app/Contents/MacOS/Tor/*" -Destination "Tor/osx64/" -Exclude "PluggableTransports"

    Write-Output "# Extract Tor binary for linux (arm64)."
    mkdir -p "Tor/linux_arm64/"
    Copy-item -Recurse -Force -Path "TorBrowser/linux_arm64/tor-browser/Browser/TorBrowser/Tor/*" -Destination "Tor/linux_arm64/" -Exclude "PluggableTransports"

    Write-Output "# Extract Tor binary for linux (x86_64)."
    mkdir -p "Tor/lin64/"
    Copy-item -Recurse -Force -Path "TorBrowser/linux_x86_64/tor-browser/Browser/TorBrowser/Tor/*" -Destination "Tor/lin64/" -Exclude "PluggableTransports"
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

  if ($skipReplacingGeoIpFiles) {
    Write-Output "# Skip replacing geoip files."
  } else {
    $destination = Join-Path -Resolve "$PSScriptRoot" ".." ".." "Tor" "Geoip"
    Write-Output "# Replace geoip files in folder '$destination'."
    Copy-item -Force -Path "TorBrowser/linux_x86_64/tor-browser/Browser/TorBrowser/Data/Tor/geoip*" -Destination "$destination/"
  }

  Write-Output "# Done."
}
finally {
    # Restore the previous location.
    Set-Location -Path $prevPwd | out-null
}
