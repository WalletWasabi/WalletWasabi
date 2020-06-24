# Abstract

This document lists all the officially supported softwares and devices by Wasabi Wallet. This means that Wasabi makes tests on those systems and put all the efforts to make it work and maintain compatibility. One of our main goals is to not break the user space so we have to set up boundaries that we can responsible maintain. This does not neccesary mean that systems those are not listed will not work - they might work but we not give support for them. There are a lot of systems out there and more to come we have focus our priorities.

# Offcially Supported Operating systems

MacOs 10.13+
Windows 10
Ubuntu


# Officially Supported Hardware wallets

- ColdCard MK1
- ColdCard MK2
- ColdCard MK3
- Ledger Nano S

# FAQ

## Operating systems

Wasabi dependencies and requirements
- .NET Core dependencies and requirements can be found [here](https://github.com/dotnet/core/blob/master/release-notes/3.1/3.1-supported-os.md)
- Avalonia dependencies and requirements can be found [here](https://github.com/AvaloniaUI/Avalonia/wiki/Runtime-Requirements)
- NBitcoin dependencies and requirements are the same with .NET Core. 
- Bitcoin Knots (same requirement as Bitcoin Core) [reqs](https://bitcoin.org/en/bitcoin-core/features/requirements#system-requirements)

## Hardware wallets

Wasabi is using [HWI](https://github.com/bitcoin-core/HWI) as a bridge between the wallet and the hardware. Unfortunately some hardware wallets that supported by HWI, implemented custom workflows those are not compatible with the Wallet.

