![](https://i.imgur.com/4GO7nnY.png)

Wasabi Wallet, formerly known as HiddenWallet is a [ZeroLink](https://github.com/nopara73/ZeroLink) compliant Bitcoin wallet. We are dedicated to restore Bitcoin's fungibility and provide the highest possible privacy for our users.  
HiddenWallet's code is archived in the [hiddenwallet-v0.6](https://github.com/zkSNACKs/WalletWasabi/tree/hiddenwallet-v0.6) branch of this repository.

| Code Quality | Windows Tests | Linux Tests | OSX Tests | License |
| :----| :---- | :------ | :------| :------ |
| [![CodeFactor][9]][10] | [![Windows build status][1]][2] | [![Linux build status][3]][4] | [![OSX build status][5]][6] |[![GitHub license][7]][8] |

[1]: https://ci.appveyor.com/api/projects/status/70j293muovayg516?svg=true
[2]: https://ci.appveyor.com/project/zkSNACKs/walletwasabi
[3]: https://travis-matrix-badges.herokuapp.com/repos/zkSNACKs/WalletWasabi/branches/master/1
[4]: https://travis-ci.org/zkSNACKs/WalletWasabi
[5]: https://travis-matrix-badges.herokuapp.com/repos/zkSNACKs/WalletWasabi/branches/master/2
[6]: https://travis-ci.org/zkSNACKs/WalletWasabi
[7]: https://img.shields.io/github/license/zkSNACKs/WalletWasabi.svg
[8]: https://github.com/zkSNACKs/WalletWasabi/blob/master/LICENSE.md
[9]: https://www.codefactor.io/repository/github/zksnacks/walletwasabi/badge
[10]: https://www.codefactor.io/repository/github/zksnacks/walletwasabi

# [Get The Latest Stable Release](https://github.com/zkSNACKs/WalletWasabi/releases)

# How To Run?

## Get The Requirements

1. Get Git: https://git-scm.com/downloads
2. Get .NET Core 2.1 SDK: https://www.microsoft.com/net/download (Note, you can disable .NET's telemetry by typing `export DOTNET_CLI_TELEMETRY_OPTOUT=1` on Linux and OSX or `set DOTNET_CLI_TELEMETRY_OPTOUT=1` on Windows.)
  
## Get Wasabi

Clone & Restore & Build

```sh
git clone https://github.com/zkSNACKs/WalletWasabi.git --recursive
cd WalletWasabi/WalletWasabi.Gui
dotnet build
```

## Run Wasabi

Run Wasabi with `dotnet run` from the `WalletWasabi.Gui` folder.

## Update Wasabi

```sh
git pull
git submodule update --init --recursive 
```

### Notes:

- Configuration, wallet and similar files can be found in `%appdata%\WalletWasabi` folder on Windows and in `~/.walletwasabi` folder on Linux/OSX.
- Note, we have OSX bugs in our GUI library, which we are currently fixing. You may follow our progress here: https://github.com/AvaloniaUI/Avalonia/pull/1789
- If you already have Tor and it's out of date, then you'll get many error messages. Make sure it's not out of date: Check Tor version: `tor --version`. If it's not at least `0.3.2.2`, then see [this writeup](https://github.com/zkSNACKs/WalletWasabi/issues/606#issuecomment-412470662) on how to update it.
