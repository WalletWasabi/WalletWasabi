## How to upgrade bundled Tor version in Wallet Wasabi

1. Check if there is a new Tor Browser version.
    * The Tor Browser changelog can be found here: https://gitweb.torproject.org/builders/tor-browser-build.git/plain/projects/tor-browser/Bundle-Data/Docs/ChangeLog.txt
    * The Tor changelog can be found here: https://gitweb.torproject.org/tor.git/plain/ChangeLog
2. [Download](https://www.torproject.org/download/) the latest stable Tor Browsers for the following platforms:
    * Windows x64
    * Linux x64
    * macOS x64 (macOS arm64 uses these binaries too)

### Windows

1. Open `torbrowser-install-win64-<version>_en-US.exe` file in a zip decompressing tool (like 7zip).
1. Navigate to `Browser\TorBrowser\Tor` folder in the archive.
1. Unzip it to `WalletWasabi\Microservices\Binaries\win64\Tor` folder.
    * Do not copy the `PluggableTransports` folder.

### Linux

1. Open `tor-browser-linux64-<version>_en-US.tar.xz` file in a zip decompressing tool (like 7zip).
1. Navigate to `tor-browser_en-US\Browser\TorBrowser\Tor` folder in the archive.
1. Unzip it to `WalletWasabi\Microservices\Binaries\lin64\Tor` folder.
    * Do not copy the `PluggableTransports` folder.

### macOS

1. Open `TorBrowser-<version>-osx64_en-US.dmg` file in a zip decompressing tool (like 7zip).
1. Navigate to `Tor Browser.app\Contents\MacOS\Tor` folder in the archive.
1. Unzip it to `WalletWasabi\Microservices\Binaries\osx64\Tor` folder but preserve the `Tor` file!
    * Do not copy the `PluggableTransports` folder.

### Upgrade Geoip files

1. Open `torbrowser-install-win64-<version>_en-US.exe` file in a zip decompressing tool (like 7zip).
1. Navigate to `Browser\TorBrowser\Data\Tor` folder in the archive.
1. Unzip it to `WalletWasabi\WalletWasabi\Tor\Geoip` folder.
