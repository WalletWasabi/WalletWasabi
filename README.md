# Hidden Wallet
Privacy focused, full block downloading SPV Bitcoin wallet with TumbleBit support.

## For developers  
  
Requirements: git, .net core, node.js, npm  
  
1. `git clone https://github.com/nopara73/HiddenWallet.git`
2. `cd HiddenWallet\HiddenWallet\HiddenWallet.API`  
3. `dotnet restore`  
4. `dotnet build`
5. `dotnet publish --output bin/dist/current-target`
6. `cd ..\HiddenWallet.GUI`
7. `npm install`
8. `npm start`
