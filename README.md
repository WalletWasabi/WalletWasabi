![](https://i.imgur.com/4GO7nnY.png)

| Code Quality           | Windows Tests           | Linux Tests             | macOS Tests             | License                   |
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

[Wasabi Wallet](https://wasabiwallet.io) is an open-source, non-custodial, privacy-focused Bitcoin wallet for desktop, that implements [Chaumian CoinJoin](https://github.com/nopara73/ZeroLink/#ii-chaumian-coinjoin).

The main privacy features on the network level:
- Tor-only by default.
- BIP 158 block filters for private light client.
- Opt-in connection to user full node.

and on the blockchain level:
- Intuitive ZeroLink CoinJoin integration.
- Superb coin selection and labeling.
- Dust attack protections.

For more information, please check out the [Wasabi Documentation](https://docs.wasabiwallet.io), an archive of knowledge about the nuances of Bitcoin privacy and how to properly use Wasabi.


# [Download Wasabi](https://github.com/zkSNACKs/WalletWasabi/releases)

![](https://i.imgur.com/kpjT9ZV.png)

For step by step instructions of PGP verification and package installation, see the [documentation](https://docs.wasabiwallet.io/using-wasabi/InstallPackage.html)

# Build From Source Code

## Get The Requirements

1. Get Git: https://git-scm.com/downloads
2. Get .NET Core 3.0 SDKs: https://www.microsoft.com/net/download (Note, you can disable .NET's telemetry by typing `export DOTNET_CLI_TELEMETRY_OPTOUT=1` on Linux and macOS or `setx DOTNET_CLI_TELEMETRY_OPTOUT 1` on Windows.)

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
