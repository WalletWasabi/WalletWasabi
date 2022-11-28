# Abstract

This document lists all the officially supported software and devices by Wasabi Wallet. This means that Wasabi is tested on those systems, and we put all the efforts to make it work and maintain compatibility. One of our main goals is to not break the user-space, so we have to set up boundaries that we can responsibly maintain. This does not necessarily mean that systems that are not listed will not work - they might work, but we do not officially support them. There are a lot of potentially supported systems out there and more to come, but we can only promise support and stability on platforms that our dependencies support, too.

# Officially Supported Operating Systems

- Windows 10 1607+
- macOs 10.15+
- Ubuntu 16.04, 18.04, 20.04+
- Fedora 33+
- Debian 10+

# Officially Supported Hardware Wallets

- ColdCard MK1
- ColdCard MK2
- ColdCard MK3
- ColdCard MK4
- Ledger Nano S
- Ledger Nano S Plus
- Ledger Nano X
- Trezor Model T

# Officially Supported Architectures

- x64

# FAQ

## What are the bottlenecks of officially supporting Operating Systems?

Wasabi dependencies are:
- .NET 6.0 [reqs](https://github.com/dotnet/core/blob/main/release-notes/6.0/supported-os.md).
- Avalonia [reqs](https://github.com/AvaloniaUI/Avalonia/wiki/Runtime-Requirements).
- NBitcoin dependencies and requirements are the same as .NET 6.0.
- Bitcoin Knots (same requirements as Bitcoin Core) [reqs](https://bitcoin.org/en/bitcoin-core/features/requirements#system-requirements).

## What are the bottlenecks of officially supporting Hardware Wallets?

Wasabi dependencies are:
- [HWI](https://github.com/bitcoin-core/HWI), check the [device support](https://github.com/bitcoin-core/HWI#device-support) list there. Some hardware wallets supported by HWI are still not compatible with Wasabi Wallet because they implemented custom workflows.

## What about Tails and Whonix?

It is currently not possible to use Wasabi on Tails or Whonix.
That is because Wasabi uses the Tor control port, which is not exposed in these operating systems.
