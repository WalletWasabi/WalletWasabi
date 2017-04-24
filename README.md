# Hidden Wallet
Privacy focused, full block downloading SPV Bitcoin wallet with TumbleBit support.

## For developers  
  
Requirements: git, .net core, node.js, npm  
  
1. `git clone https://github.com/nopara73/HiddenWallet.git`
2. `cd HiddenWallet.API`  
3. `dotnet restore`  
4. `dotnet build`
5. For win7: `dotnet publish -r win7-x64 --output bin/dist/win` (for other platforms: https://pub.scotch.io/@rui/how-to-build-a-cross-platform-desktop-application-with-electron-and-net-core)  
6. `cd ..`
7. `cd HiddenWallet.GUI`
8. `npm install`
9. `npm start`
