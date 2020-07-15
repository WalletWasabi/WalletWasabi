# Guide for deterministic builds

The term *deterministic builds* is [defined](https://reproducible-builds.org/) as follows:

> Reproducible [or deterministic] builds are a set of software development practices that create an independently-verifiable path from source to binary code.

This guide describes how to reproduce Wasabi's builds. If you get stuck with these instructions, take a look at [how to build Wasabi from source code](https://docs.wasabiwallet.io/using-wasabi/BuildSource.html).

**Warning:** Reproducible builds were introduced in [1.1.3 release](https://github.com/zkSNACKs/WalletWasabi/releases/tag/v1.1.3), you cannot use these instructions for older versions!

## 1. Assert correct environment

In order to reproduce Wasabi's builds, you need [git](https://git-scm.com/) package, Windows 10, and the version of .NET Core SDK that was used by the Wasabi team to produce given Wasabi Wallet release. The latest version of .NET Core SDK is always used, unless specified otherwise in the release notes of Wasabi Wallet. You can get it here https://dotnet.microsoft.com/download/dotnet-core.

## 2. Reproduce builds

You can see the list of Wasabi releases here: https://github.com/zkSNACKs/WalletWasabi/releases. Please note that each release has a version and a git hash assigned. The git hash is useful in the following instructions:

```sh
git clone https://github.com/zkSNACKs/WalletWasabi.git
cd WalletWasabi
git checkout <SHA-1-hash-of-the-release>
cd WalletWasabi.Packager
dotnet restore
dotnet build
dotnet run -- --onlybinaries
```

These commands will produce Wasabi's binaries for Windows, macOS and Linux from source code. Also, for your convenience, a new file explorer window will navigate you to the binaries location - i.e. `WalletWasabi\\WalletWasabi.Gui\\bin\\dist`.

![](https://i.imgur.com/8XAQzz4.png)

## 3. Verify builds

Now, we will attempt to verify the binaries you have just compiled with the officially distributed binaries on https://wasabiwallet.io website. Please download those packages from the website, you should see the following files in your File Explorer:

![](https://i.imgur.com/aI9Kx0c.png)

Please note that to completely verify Wasabi packages for all supported platforms, you actually need machines with Windows, Linux and macOS operating systems. If you don't have so many physical machines, you can use any virtualization sofware packages like [VirtualBox](https://www.virtualbox.org/), [VMWare](https://www.vmware.com/), etc.

### Windows

* Install Wasabi using `Wasabi-<version>.msi` file. It will install to `C:\Program Files\WasabiWallet` directory.
* Start `cmd` or Powershell and navigate to the `dist` directory.
* Execute the following command:
  ```sh
  git diff --no-index "win7-x64" "C:\Program Files\WasabiWallet"
  ```
  and make sure that there is **NO** difference reported by the command.

### Linux & macOS

You can use the [Windows Subsystem for Linux](https://docs.microsoft.com/en-us/windows/wsl/) to verify all the packages in one go. At the time of writing this guide we provide `.tar.gz` and `.deb` packages for Linux and `.dmg` package for macOS.  
Install the `.deb` package and extract the `tar.gz` and `.dmg` packages, then compare them with your build.

After [installing WSL](https://docs.microsoft.com/en-us/windows/wsl/install-win10), just type `wsl` in File Explorer where your downloaded and built packages are located.

![](https://i.imgur.com/yRUjxvG.png)

#### .deb

```sh
sudo dpkg -i Wasabi-1.1.6.deb
git diff --no-index linux-x64/ /usr/local/bin/wasabiwallet/
```

#### .tar.gz

```sh
tar -pxzf WasabiLinux-1.1.6.tar.gz
git diff --no-index linux-x64/ WasabiLinux-1.1.6
```

#### .dmg

You will need to install `7z` (or something else) to extract the `.dmg`. You can do that using `sudo apt install p7zip-full` command.

```sh
7z x Wasabi-1.1.6.dmg -oWasabiOsx
git diff --no-index osx-x64/ WasabiOsx/Wasabi\ Wallet.App/Contents/MacOS/
```
