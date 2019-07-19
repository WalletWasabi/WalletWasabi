![](https://i.imgur.com/4GO7nnY.png)

Wasabi Wallet, formerly known as HiddenWallet is a [ZeroLink](https://github.com/nopara73/ZeroLink) compliant Bitcoin wallet. We are dedicated to restore Bitcoin's fungibility and provide the highest possible privacy for our users.
HiddenWallet's code is archived in the [hiddenwallet-v0.6](https://github.com/zkSNACKs/WalletWasabi/tree/hiddenwallet-v0.6) branch of this repository.

| Code Quality           | Windows Tests           | Linux Tests             | OSX Tests               | License                   |
| :----------------------| :-----------------------| :-----------------------| :-----------------------| :-------------------------|
| [![CodeFactor][9]][10] | [![Build Status][1]][2] | [![Build Status][3]][4] | [![Build Status][5]][6] | [![GitHub license][7]][8] |

[1]: https://dev.azure.com/zkSNACKs/Wasabi/_apis/build/status/Wasabi.Windows?branchName=master
[2]: https://dev.azure.com/zkSNACKs/Wasabi/_build?definitionId=3
[3]: https://dev.azure.com/zkSNACKs/Wasabi/_apis/build/status/Wasabi.Linux?branchName=master
[4]: https://dev.azure.com/zkSNACKs/Wasabi/_build?definitionId=1
[5]: https://dev.azure.com/zkSNACKs/Wasabi/_apis/build/status/Wasabi.Osx?branchName=master
[6]: https://dev.azure.com/zkSNACKs/Wasabi/_build?definitionId=2
[7]: https://img.shields.io/github/license/zkSNACKs/WalletWasabi.svg
[8]: https://github.com/zkSNACKs/WalletWasabi/blob/master/LICENSE.md
[9]: https://www.codefactor.io/repository/github/zksnacks/walletwasabi/badge
[10]: https://www.codefactor.io/repository/github/zksnacks/walletwasabi


# [Download Wasabi](https://github.com/zkSNACKs/WalletWasabi/releases)

![](https://i.imgur.com/kpjT9ZV.png)

# Build From Source Code

## Get The Requirements

1. Get Git: https://git-scm.com/downloads
2. Get .NET Core 2.2 SDK: https://www.microsoft.com/net/download (Note, you can disable .NET's telemetry by typing `export DOTNET_CLI_TELEMETRY_OPTOUT=1` on Linux and OSX or `set DOTNET_CLI_TELEMETRY_OPTOUT=1` on Windows.)

## Get Wasabi

Clone & Restore & Build

```sh
git clone https://github.com/zkSNACKs/WalletWasabi.git
cd WalletWasabi/WalletWasabi.Gui
dotnet build
```

## Run Wasabi

Run Wasabi with `dotnet run` from the `WalletWasabi.Gui` folder.

## Update Wasabi

```sh
git pull
```
