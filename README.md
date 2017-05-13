# Hidden Wallet
Privacy focused, full block downloading SPV Bitcoin wallet with TumbleBit support. This repository is a thin GUI, the bulk of the project is at https://github.com/nopara73/HBitcoin/.  
  
Pizza and/or coffee is welcome: [186n7me3QKajQZJnUsVsezVhVrSwyFCCZ](https://www.smartbit.com.au/address/186n7me3QKajQZJnUsVsezVhVrSwyFCCZ).
  
**Important Note:** Until the stable version is out, one shold not use it on the mainnet, but only on the testnet for testing purposes. Please load me with a lot of work by opening countless GitHub issues on literally anything or send me email to `adam.ficsor73@gmail.com`.
  
## Status & Roadmap
1. [Full Block Downloading SPV](https://medium.com/@nopara73/bitcoin-privacy-landscape-in-2017-zero-to-hero-guidelines-and-research-a10d30f1e034) - **Ready, unstable.**  This feature provides full node level privacy against network analysis with SPV security.  
2. [TumbleBit integration, Classi Tumbler Mode](https://hackernoon.com/understanding-tumblebit-part-1-making-the-case-823d786113f3) - **Next up.** TumbleBit is a Bitcoin mixer where not even the Tumbler can steal your coins, nor deanonymize you.   
3. [Transaction Filtered Block Downloading](https://medium.com/@nopara73/full-node-level-privacy-even-for-mobile-wallets-transaction-filtered-full-block-downloading-wallet-16ef1847c21)  - 10-100x performance increase, while still keeping full node level privacy against network analysis, but replacing SPV security to trusted full node security.  
4. Release stable version.  
5. TumbleBit Paymen Hub Mode? JoinMarket integration? Stealth addresses support?

## Screenshots 

![Generate](https://github.com/nopara73/HiddenWallet/blob/master/HiddenWallet/HiddenWallet.Docs/HwDecryptingScreenshot.png)  

![BuildTransaction](https://github.com/nopara73/HiddenWallet/blob/master/HiddenWallet/HiddenWallet.Docs/HwBuildTransactionScreenshot.png)  

![History](https://github.com/nopara73/HiddenWallet/blob/master/HiddenWallet/HiddenWallet.Docs/HwHistoryScreenshot.png)  


## Release  
Note: Binaries are only available to Windows, for Linux and OSX you need to build from source code (see "Building from source code" section).  

## Configuration

After first running the software, it will generate a `Config.json` file for you:  
```
{
  "WalletFilePath": "Wallets\\Wallet.json",
  "Network": "Main",
  "CanSpendUnconfirmed": "False"
}
```  
Make sure the network is set to `"TestNet"` and for easier testing set enable the spending of unconfirmed transactions by setting the value to `"true"`.  
If you've already generated a walet accidentally on the main net, then you want to change the wallet file path, too, for example to `"WalletTestNet.json"`.  
Since testnet coins have no value, you can acquire them freely and quickly: http://lmgtfy.com/?q=get+testnet+faucet

## Building from source code  
  
### Requirements:  
- [Git](https://git-scm.com/downloads)  
- [.NET Core](https://www.microsoft.com/net/core)  
- [Node.js](https://nodejs.org/en/download/)
- Tor: On Linux and OSX make sure "tor" is in your PATH!
  
### Step by step
  
1. `git clone https://github.com/nopara73/HiddenWallet.git`
2. `cd HiddenWallet/HiddenWallet/HiddenWallet.API`  
3. `dotnet restore`  
4. `dotnet build`
5. `dotnet publish -r win7-x64 --output bin/dist/current-target`. Find your platform identifier [here](https://github.com/dotnet/docs/blob/master/docs/core/rid-catalog.md#windows-rids) and replace `win7-x64`.
If you get an error here, don't worry, just add your platform identifier to the `<RuntimeIdentifiers>` tag in the `HiddenWallet.API/HiddenWallet.API.csproj file).  
6. (Only on Windows) - Copy and unzip `HiddenWallet.Packager/tor.zip` to `HiddenWallet.API/bin/dist/current-target` directory. (Quick note on unzipping: the final path to tor.exe has to be: `current-target/tor/Tor/tor.exe` and not: `current-target/tor/tor/Tor/tor.exe`.)
7. `cd ../HiddenWallet.GUI`
8. `npm install`
9. `npm start`
10. Check out the Configuration section above.
