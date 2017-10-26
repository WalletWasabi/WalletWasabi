## Releases  
[Download for Windows.](https://github.com/nopara73/HiddenWallet/releases)  
For Linux and OSX you need to build from the source code, [see instructions below](https://github.com/nopara73/HiddenWallet#building-from-source-code).

# Hidden Wallet

Privacy focused, soon [ZeroLink](https://github.com/nopara73/ZeroLink) compliant Bitcoin wallet.  
  
## Support

186n7me3QKajQZJnUsVsezVhVrSwyFCCZ  
[![QR Code](http://i.imgur.com/grc5fBP.png)](https://www.smartbit.com.au/address/186n7me3QKajQZJnUsVsezVhVrSwyFCCZ)
  
**Important Note:** Until the stable version is out, one should not use it on the mainnet, but only on the testnet for testing purposes. Please give me a lot of work by opening GitHub issues or send me an email to `adam.ficsor73@gmail.com`.
  
## Status & Roadmap
1. [Full Block Downloading SPV](https://medium.com/@nopara73/bitcoin-privacy-landscape-in-2017-zero-to-hero-guidelines-and-research-a10d30f1e034) - **Ready, unstable.**  This feature provides full node level privacy against network analysis with SPV security.  
2. [ZeroLink compliance](https://github.com/nopara73/ZeroLink/) - **Next up.** ZeroLink is a Bitcoin Fungibility Framework, it includes Wallet Privacy requirements and a mixing technique: Chaumian CoinJoin.   
3. [Transaction Filtered Block Downloading](https://medium.com/@nopara73/full-node-level-privacy-even-for-mobile-wallets-transaction-filtered-full-block-downloading-wallet-16ef1847c21)  - 10-100x performance increase, while still keeping full node level privacy against network analysis, but replacing SPV security to trusted full node security.  
4. Release stable version.  
5. TumbleBit Paymen Hub Mode? JoinMarket integration? Stealth addresses support?

## Screenshots 

![BuildTransaction](https://i.imgur.com/EUX4zT4.png)  

![History](https://i.imgur.com/IQ0M37R.png)    

## Configuration

HiddenWallet is working in your APPDATA folder on Windows and in your HOME folder on Linux and OSX.  
After first running the software, it will generate a `Config.json` file for you:  
```
{
  "WalletFilePath": "Wallets\\Wallet.json",
  "Network": "Main",
  "CanSpendUnconfirmed": "False"
}
```  
For testing, set the network to `"TestNet"` and enable the spending of unconfirmed transactions by setting its value to `"True"`.  
If you've already generated a wallet on the mainnet, then you want to change the default wallet file path, too, for example to `"WalletTestNet.json"`.  
Since testnet coins have no value, you can acquire them freely and quickly: http://lmgtfy.com/?q=get+testnet+faucet  
**Update:** From release of HiddenWallet v0.5 the wallet only generates [Bech32 SegWit addresses](https://github.com/bitcoin/bips/blob/master/bip-0173.mediawiki). At the time of writing it is not intuitive how to acquire testnet faucets to such addresses. Hopefully by the time you are reading this, it will not be the case anymore. From Bitcoin Core v0.15.1 you can send transactions to Bech32 addresses. So as a workaround you can use your Core node, receive faucets there and forward it to HiddenWallet.  

## Building From Source Code  
  
### Requirements:  
- [Git](https://git-scm.com/downloads)  
- [.NET Core](https://www.microsoft.com/net/core)  
- [Node.js](https://nodejs.org/en/download/)
- Tor: On Linux and OSX make sure "tor" is in your PATH!
  
### Step By Step
  
1. `git clone https://github.com/nopara73/HiddenWallet.git`
2. `cd HiddenWallet/HiddenWallet.Daemon`  
3. `dotnet publish -r win10-x64 --output bin/dist/current-target`. Find your platform identifier [here](https://github.com/dotnet/docs/blob/master/docs/core/rid-catalog.md#windows-rids) and replace `win7-x64`.
If you get an error here, don't worry, just add your platform identifier to the `<RuntimeIdentifiers>` tag in the `HiddenWallet.Daemon/HiddenWallet.Daemon.csproj` file).  
4. (Only on Windows) - Copy and unzip `HiddenWallet.Packager/tor.zip` to `HiddenWallet.Daemon/bin/dist/current-target` directory. (Quick note on unzipping: the final path to tor.exe has to be: `current-target/tor/Tor/tor.exe` and not: `current-target/tor/tor/Tor/tor.exe`.)
5. `cd ../HiddenWallet.Gui`
6. `npm install`
7. `npm start`
8. Check out the [Configuration section](https://github.com/nopara73/HiddenWallet#configuration) above.

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
