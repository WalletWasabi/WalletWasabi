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

## Build & Run

1. Get Git: https://git-scm.com/downloads
2. Get .NET Core: https://www.microsoft.com/net/download/dotnet-core/
3. [OSX] Get Brew: https://stackoverflow.com/a/20381183/2061103
4. Get Tor:  
  [Windows] Install the Tor Expert Bundle: https://www.torproject.org/download/  
  [Linux] `apt-get install tor`  
  [OSX] `brew install tor`  
5. Run Tor:  
  [Windows] Run `tor.exe`.  
  [Linux&OSX] Type `tor` in terminal.  
6. Clone, Build & Run
```sh
git clone https://github.com/zkSNACKs/WalletWasabi
cd WalletWasabi
git submodule update --init --recursive
dotnet restore && dotnet build
cd WalletWasabi.Gui
dotnet run
```

## Update

```sh
git pull
git submodule update --init --recursive 
```

### Notes:

- Configuration, wallet and similar files can be found in `%appdata%\WalletWasabi` folder on Windows and in `~/.walletwasabi` folder on Linux/OSX.
