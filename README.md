# Hidden Wallet
Privacy focused, [full block downloading SPV](https://medium.com/@nopara73/bitcoin-privacy-landscape-in-2017-zero-to-hero-guidelines-and-research-a10d30f1e034) Bitcoin wallet with [TumbleBit](https://hackernoon.com/understanding-tumblebit-part-1-making-the-case-823d786113f3) support.  

## Building from source code  
  
### Requirements:  
- [Git](https://git-scm.com/downloads)  
- [.NET Core](https://www.microsoft.com/net/core)  
- [Node.js](https://nodejs.org/en/download/)
- Tor (more specific instructions later)

### Step by step
  
1. `git clone https://github.com/nopara73/HiddenWallet.git`
2. `cd HiddenWallet\HiddenWallet\HiddenWallet.API`  
3. `dotnet restore`  
4. `dotnet build`
5. `dotnet publish --output bin/dist/current-target`
6. `cd ..\HiddenWallet.GUI`
7. `npm install`
8. `npm start`
