![](https://i.imgur.com/4GO7nnY.png)

Wallet Wasabi, formerly known as HiddenWallet is a [ZeroLink](https://github.com/nopara73/ZeroLink) compliant Bitcoin wallet. We are dedicated to restore Bitcoin's fungibility and provide the highest possible privacy for our users.  
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
[10]: https://www.codefactor.io/repository/github/zksnacks/walletwasabi/badge

## Build & Test

1. Get .NET Core.
2. Download the Tor Experd Bundle. (Not the Tor Browser.) https://www.torproject.org/download/download
3. Run `tor.exe`.
4. Fire up command line:
```
git clone https://github.com/zkSNACKs/WalletWasabi
cd WalletWasabi
dotnet restore && dotnet build
cd WalletWasabi.Tests
dotnet test
```

### Notes:

1. `dotnet test` takes about 3 minutes, the first time it'll take 1-2 minutes longer, because it downloads `bitcoind`. This daemon is used by the tests in regression test mode to simulate the Bitcoin network.  

2. If you'd happen to terminate the tests immaturely, make sure you also terminate the `bitcoind` daemon process from the Task Manager, otherwise next time your tests would fail.

2. Tests are using `%appdata%\WalletWasabi` folder to log and for other things, so you can delete it after you no longer wish to do anything with the software anymore.

## Roadmap

**Goal:** Stability, feature minimalization.

It is expected for the proposed timeline to take roughly 1.5 times longer.

### 1. Wallet Stage

- [x] **1. Wallet Back End.** *2 weeks. NBitcoin, Bitcoin Core RPC, ASP.NET.* Build back end.
- [x] **2. Small Tor Library.** *1 week. Tor, .NET Core, cross platform.* Build a small Tor library based on DotNetTor, that removes features those are unrelated to the wallet and makes the rest more stable.
- [x] **3. Key Manager.** *1 week. NBitcoin.* Build a new, high performance key manager with accounts, labelling, etc...
- [x] **4. Wallet Client.** *1 month. NBitcoin.* Build a high performance client that can work with the new back end, which is built for client side filtering.

  Depends on:
  - [x] ALL previous items
  
### 2. Privacy Stage

- [x] **5. ZeroLink Research.** *1 month. Bitcoin privacy, cryptography.* Research ZeroLink, based on technological advancements.
- [x] **6. ZeroLink Coordinator.** *2 weeks. NBitcoin, Bitcoin Core RPC.* Revise the ZeroLink Coordinator code based on ZeroLink v2 Revision.

  Depends on:
  - [x] ZeroLink Research
  
- [ ] **7. ZeroLink Client.** *2 weeks. NBitcoin.* Revise the ZeroLink Client code based on ZeroLink v2 Revision.

  Depends on:
  - [x] ZeroLink Research
  - [x] ZeroLink Coordinator

### 3. User Experience Stage

- [ ] **8. GUI.** *1 month. Electron, front end, Bitcoin.* Redesign the user experience and build it.

  Depends on:
  - [ ] ALL previous items
  
### 4. Deployment Stage
  
- [ ] **9. Documentation.** *1 week. Bitcoin.* Create documentation.

  Depends on:
  - [ ] ALL previous items
  
- [ ] **10. Internal Testing.** *2 weeks. Bitcoin or .NET or front end.* Test the software and fix the bugs (if there is any haha).

  Depends on:
  - [ ] ALL previous items, except Documentation
  
- [ ] **11. Deploy To Mainnet.** *2 weeks. .NET Core, ASP.NET Core deployment.* Deploy the software to Bitcoin Mainnet.

  Depends on:
  - [ ] ALL previous items, except Documentation
  
- [ ] **12. Mainnet Beta Testing.** *2 weeks.* It's rather a marketing phase. The goal is to get at least 1 round done with > 100 user.

  Depends on:
  - [ ] ALL previous items
  
