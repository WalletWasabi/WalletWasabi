## Technical overview
  
- [HBitcoin](https://github.com/nopara73/HBitcoin/) - Privacy focused Bitcoin library on top of [NBitcoin](https://github.com/MetacoSA/NBitcoin/) for .NET Core, this is where the wallet logic happens. HiddenWallet is using Tor through the [DotNetTor](https://github.com/nopara73/DotNetTor) library, where it's needed to protect the user's privacy.
- [HiddenWallet.API](https://github.com/nopara73/HiddenWallet/tree/master/HiddenWallet/HiddenWallet.API) - A thin HTTP API layer, that connects HBitcoin to the GUI layer, written in (ASP.NET Core WebApi).
- [HiddenWallet.GUI](https://github.com/nopara73/HiddenWallet/tree/master/HiddenWallet/HiddenWallet.GUI) - A thin user interface, [using Electron](https://github.com/electron/electron). (Node.js + web technologies)
- [HiddenWallet.Packager](https://github.com/nopara73/HiddenWallet/tree/master/HiddenWallet/HiddenWallet.Packager) - To automate the packaging of the releases.

# References

- [HTTP API Specification](https://github.com/nopara73/HiddenWallet/blob/master/HiddenWallet/HiddenWallet.Docs/ApiSpecs.md)
- [Ports in use](https://github.com/nopara73/HiddenWallet/blob/master/HiddenWallet/HiddenWallet.Docs/Ports.md)
