# Hidden Wallet

Privacy focused, soon [ZeroLink](https://github.com/nopara73/ZeroLink) compliant Bitcoin wallet.  

## Releases  
[Download for Windows and Linux.](https://github.com/nopara73/HiddenWallet/releases)  
For OSX you need to build from the source code, [see instructions below](https://github.com/nopara73/HiddenWallet#building-from-source-code).
  
## Support

### [Contributions Spent On](https://github.com/nopara73/HiddenWallet/blob/master/HiddenWallet.Documentation/DonationsSpentOn.md)

186n7me3QKajQZJnUsVsezVhVrSwyFCCZ  
[![QR Code](http://i.imgur.com/grc5fBP.png)](https://www.smartbit.com.au/address/186n7me3QKajQZJnUsVsezVhVrSwyFCCZ)
  
**Important Note:** Until the stable version is out, one should not use it on the mainnet, but only on the testnet for testing purposes. Please give me a lot of work by opening GitHub issues or send me an email to `adam.ficsor73@gmail.com`.
  
## [Testing The ZeroLink Mixer](https://github.com/nopara73/HiddenWallet/blob/master/HiddenWallet.Documentation/TestingTheZeroLinkMixer.md)
  
## Status & Roadmap
1. [Full Block Downloading SPV](https://medium.com/@nopara73/bitcoin-privacy-landscape-in-2017-zero-to-hero-guidelines-and-research-a10d30f1e034) - **Ready, unstable.**  This feature provides full node level privacy against network analysis with SPV security.  
2. [ZeroLink mixer implementation](https://github.com/nopara73/ZeroLink/) - **Ready, unstable.** ZeroLink is a Bitcoin Fungibility Framework, it includes Wallet Privacy requirements and a mixing technique: Chaumian CoinJoin.   
against network analysis, but replacing SPV security to trusted full node security.  
3. Release stable version: A. Build stable ZeroLink Mixer. B. Replace the back end with Bitcoin Core. C. Redesign the GUI.
4. [Transaction Filtered Block Downloading?](https://medium.com/@nopara73/full-node-level-privacy-even-for-mobile-wallets-transaction-filtered-full-block-downloading-wallet-16ef1847c21)  - 10-100x performance increase, while still keeping full node level privacy 
5. TumbleBit Paymen Hub Mode? JoinMarket integration? Stealth addresses support?

## Screenshots 

---
![](https://i.imgur.com/YU4JskT.png)
---
![](https://i.imgur.com/xvizcmu.png)
---

## Building From Source Code  
  
### Requirements:  
- [Git](https://git-scm.com/downloads)  
- [.NET Core](https://www.microsoft.com/net/core)  
- [Node.js](https://nodejs.org/en/download/)
- Tor: On Linux (`apt-get install tor`) and OSX (`brew install tor`) make sure "tor" is in your PATH! , 
  
### Step By Step
  
1. `git clone https://github.com/nopara73/HiddenWallet.git`
2. `cd HiddenWallet/HiddenWallet.Daemon`  
3. `dotnet restore`  
4. `dotnet publish -c Release -r win-x64 --output bin/dist/current-target`. Depending on your platform replace `win-x64` with `win-x86`, `linux-x64` or `osx-x64`.  
5. (Only on Windows) - Copy and unzip `HiddenWallet.Packager/tor.zip` to `HiddenWallet.Daemon/bin/dist/current-target` directory. (Quick note on unzipping: the final path to tor.exe has to be: `current-target/tor/Tor/tor.exe` and not: `current-target/tor/tor/Tor/tor.exe`.)
6. `cd ../HiddenWallet.Gui`
7. `npm install`
8. `npm install -g typescript`
9. `tsc`
10. `npm start`
11. Check out the [Configuration section](https://github.com/nopara73/HiddenWallet#configuration) above.

### Running The Tests

1. Download [Tor](https://www.torproject.org/download/download), (for Windows you need the Expert Bundle) and use this configuration file: [torrc](https://github.com/nopara73/DotNetTor/blob/master/torrc)  
2. Run Tor 
3. `cd HiddenWallet.Tests`  
4. `dotnet restore`  
5. `dotnet build`  
6. `dotnet test`  

*Notes:* 
- Some tests have been prefunded with testnet coins. If some funny dev messing with the wallets (sending transactions to them, spending them and such) those tests might fail, too.

## [Architechture Overview](https://github.com/nopara73/HiddenWallet/blob/master/HiddenWallet.Documentation/ArchitechtureOverview.md)
