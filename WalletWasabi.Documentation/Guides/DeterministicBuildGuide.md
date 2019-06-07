> Reproducible [or deterministic] builds are a set of software development practices that create an independently-verifiable path from source to binary code.- https://reproducible-builds.org/

This guide describes how to reproduce Wasabi's builds. If you got stuck with these instructions, take a look at how to build Wasabi from source code: https://github.com/zkSNACKs/WalletWasabi#build-from-source-code

# 1: Assert Correct Environment

In order to reproduce Wasabi's builds you need a Git, a Windows 10 and the version of .NET Core SDK that was the most recent in the time of building the release.

# 2. Reproduce Builds

```sh
git clone https://github.com/zkSNACKs/WalletWasabi.git
git checkout {hash of the release} // This works from 1.1.3 release, https://github.com/zkSNACKs/WalletWasabi/releases
cd WalletWasabi/WalletWasabi.Packager/
dotnet restore
dotnet build
dotnet run --onlybinaries
```

This will build our binaries for Windows, OSX and Linux from source code and open them in a file explorer for you.

![](https://i.imgur.com/8XAQzz4.png)

# 3. Verify Builds

You can compare our binaries with the downloads we have on the website: https://wasabiwallet.io/
In order to end-to-end verify all the downloaded packages you need a Windows, a Linux and an OSX machine.

![](https://i.imgur.com/aI9Kx0c.png)

## Windows

After you installed Wasabi from the `.msi`, it'll be in `C:\Program Files\WasabiWallet` folder. You can compare it with your build:

```sh
git diff --no-index win7-x64 "C:\Program Files\WasabiWallet"
```

## Linux && OSX

You can use the Windows Subsystem for Linux to verify all the packages in one go. At the time of writing this guide we provide a `.tar.gz` and a `.deb` package for Linux and .dmg for OSX.  
Install the `.deb` package and extract the `tar.gz` and `.dmg` packages, then compare them with your build.

After installing WSL, just type `wsl` in explorer where your downloaded and built packages are located:

![](https://i.imgur.com/yRUjxvG.png)

### .deb

```sh
sudo dpkg -i Wasabi-1.1.3.deb
git diff --no-index linux-x64/ /usr/local/bin/wasabiwallet/
```

### .tar.gz

```sh
tar -pxzf WasabiLinux-1.1.3.tar.gz
git diff --no-index linux-x64/ WasabiLinux-1.1.3
```

### .dmg

You'll need to install `7z` (or something else) to extract the `.dmg`: `sudo apt install p7zip-full`

```sh
7z x Wasabi-1.1.3.dmg -oWasabiOsx
git diff --no-index osx-x64/ WasabiOsx/Wasabi\ Wallet.App/Contents/MacOS/
```
