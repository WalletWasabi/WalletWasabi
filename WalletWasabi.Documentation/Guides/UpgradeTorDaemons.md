Check if there is a new Tor Browser version.

The Tor Browser changelog can found here: https://gitweb.torproject.org/builders/tor-browser-build.git/plain/projects/tor-browser/Bundle-Data/Docs/ChangeLog.txt

The Tor changelog can found here: https://gitweb.torproject.org/tor.git/plain/ChangeLog

Download the latest stable Tor Browser from here: https://www.torproject.org/download/

- Windows x64
- Linux x64
- macOS x64

Do not copy PluggableTransports folder!

## Windows
msi => Browser\TorBrowser\Tor

## Linux
tar.gz => tor-browser_en-US\Browser\TorBrowser\Tor

## macOS
dmg => Tor Browser.app\Contents\MacOS\Tor

Do not delete Tor file from the original folder!

## Geoip files

msi => Browser\TorBrowser\Data\Tor

## Digest creation 

WalletWasabi\WalletWasabi.Packager> dotnet run -- onlycreatedigests
Copy hashes (SHA256) without file names into WalletWasabi\WalletWasabi\TorDaemons\digests.txt (order of the hashes doesn't matter)
