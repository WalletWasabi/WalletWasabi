## Technical Overview Of Projects
  
- [HiddenWallet](https://github.com/nopara73/HiddenWallet/tree/master/HiddenWallet) - Basic Bitcoin library, on top of [NBitcoin](https://github.com/MetacoSA/NBitcoin/).  

- [HiddenWallet.FullSpvWallet](https://github.com/nopara73/HiddenWallet/tree/master/HiddenWallet.FullSpvWallet) - Previously it was a part of [HBitcoin](https://github.com/nopara73/HBitcoin/) library. This is where the wallet logic happens.  
HiddenWallet.FullSpvWallet project is using Tor through the [DotNetTor](https://github.com/nopara73/DotNetTor) library, where it's needed to protect the user's privacy.  
- [HiddenWallet.Daemon](https://github.com/nopara73/HiddenWallet/tree/master/HiddenWallet.Daemon) - A thin HTTP API layer, that connects HiddenWallet.FullSpvWallet to the GUI layer, written in (ASP.NET Core WebApi).  
- [HiddenWallet.Gui](https://github.com/nopara73/HiddenWallet/tree/master/HiddenWallet.Gui) - A thin user interface, [using Electron](https://github.com/electron/electron). (Node.js + web technologies)  
- [HiddenWallet.Packager](https://github.com/nopara73/HiddenWallet/tree/master/HiddenWallet.Packager) - To automate the packaging of the releases.  

# References

- [HTTP API Specification](https://github.com/nopara73/HiddenWallet/blob/master/HiddenWallet.Documentation/ApiSpecs.md)
- [Ports in use](https://github.com/nopara73/HiddenWallet/blob/master/HiddenWallet.Documentation/Ports.md)
