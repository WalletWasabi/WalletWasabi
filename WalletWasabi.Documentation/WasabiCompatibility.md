# Abstract

This document lists all the officially supported software and devices by Wasabi Wallet. This means that Wasabi is tested on those systems, and we put all the efforts to make it work and maintain compatibility. One of our main goals is to not break the user-space, so we have to set up boundaries that we can responsibly maintain. This does not necessarily mean that systems that are not listed will not work - they might work, but we do not officially support them. There are a lot of potentially supported systems out there and more to come, but we can only promise support and stability on platforms that our dependencies support, too.

# Officially Supported Operating Systems

- Windows 10 1607+ (except 1703)
- macOs 10.15+
- Ubuntu 16.04, 18.04, 20.04+
- Fedora 33+
- Debian 10+

# Officially Supported Hardware wallets

- ColdCard MK1
- ColdCard MK2
- ColdCard MK3
- Ledger Nano S
- Ledger Nano X

# Officially Supported Architectures

- x64

# FAQ

## What are the bottlenecks of officially supporting Operating Systems?

Wasabi dependencies are:
- .NET Core [reqs](https://github.com/dotnet/core/blob/master/release-notes/3.1/3.1-supported-os.md).
- Avalonia [reqs](https://github.com/AvaloniaUI/Avalonia/wiki/Runtime-Requirements).
- NBitcoin dependencies and requirements are the same as .NET Core.
- Bitcoin Knots (same requirements as Bitcoin Core) [reqs](https://bitcoin.org/en/bitcoin-core/features/requirements#system-requirements).

## What are the bottlenecks of officially supporting Hardware Wallets?

Wasabi dependencies are:
- [HWI](https://github.com/bitcoin-core/HWI), check the [device support](https://github.com/bitcoin-core/HWI#device-support) list there. Some hardware wallets supported by HWI are still not compatible with the Wallet because they implemented custom workflows.

## What about Whonix and Tails?

Whonix and Tails are privacy-oriented OSs, so it makes sense to use them with Wasabi Wallet. At the moment, Wasabi is working properly on these platforms, but our dependencies do not officially support them, so we cannot make promises regarding future stability.

## What about Trezor?

Trezor One and Trezor T are popular hardware wallets that Wasabi officially supported in the past. However, from April 2020 to June 2020, Trezor's new firmware releases introduced 3 backward-incompatible changes, which made us reassess our official support for the hardware wallet.
- https://github.com/bitcoin-core/HWI/pull/319
- https://github.com/zkSNACKs/WalletWasabi/issues/3734
- https://github.com/trezor/trezor-firmware/issues/1044

At the moment, Wasabi is working properly with Trezor One and Trezor T, but after resolving these issues, we must concede guaranteeing future stability is beyond our control, hence we shan't promise to continue official support forthwith.
