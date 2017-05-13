# Hidden Wallet
Privacy focused, [full block downloading SPV](https://medium.com/@nopara73/bitcoin-privacy-landscape-in-2017-zero-to-hero-guidelines-and-research-a10d30f1e034) Bitcoin wallet with [TumbleBit](https://hackernoon.com/understanding-tumblebit-part-1-making-the-case-823d786113f3) support.  

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
