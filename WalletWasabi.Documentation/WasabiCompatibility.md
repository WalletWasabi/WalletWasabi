# Abstract

This document lists all the officially supported software and devices by Wasabi Wallet. This means that Wasabi makes tests on those systems and put all the efforts to make it work and maintain compatibility. One of our main goals is to not break the user space so we have to set up boundaries that we can responsibly maintain. This does not necessarily mean that systems that are not listed will not work - they might work but we not give support for them. There are a lot of systems out there and more to come we have to focus our priorities.

# Officially Supported Operating systems

Windows 10
macOs 10.13+
Ubuntu 16.04+
Fedora 30+
Debian 9+
CentOS 7+

# Officially Supported Hardware wallets

- ColdCard MK1
- ColdCard MK2
- ColdCard MK3
- Ledger Nano S

# FAQ

## Operating systems

Wasabi might work on OSs those support these dependencies:
- .NET Core [reqs](https://github.com/dotnet/core/blob/master/release-notes/3.1/3.1-supported-os.md)
- Avalonia [reqs](https://github.com/AvaloniaUI/Avalonia/wiki/Runtime-Requirements)
- NBitcoin dependencies and requirements are the same as .NET Core. 
- Bitcoin Knots (same requirement as Bitcoin Core) [reqs](https://bitcoin.org/en/bitcoin-core/features/requirements#system-requirements)

## Hardware wallets

Wasabi is using [HWI](https://github.com/bitcoin-core/HWI) as a bridge between the wallet and the hardware. Check the compatibility list there, but some hardware wallets supported by HWI still not compatible with the Wallet because they implemented custom workflows that are not compatible.
