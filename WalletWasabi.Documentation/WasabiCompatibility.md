# Abstract

This document lists all the officially supported software and devices by Wasabi Wallet. This means that Wasabi is tested on those systems, and we put all the efforts to make it work and maintain compatibility. One of our main goals is to not break the user-space, so we have to set up boundaries that we can responsibly maintain. This does not necessarily mean that systems that are not listed will not work - they might work, but we do not officially support them. There are a lot of potentially supported systems out there and more to come, but we can only promise support and stability on platforms that our dependencies support, too.

# Officially Supported Operating Systems

- Windows 10 1607+
- Windows 11 22000+
- macOS 13.0+
- Ubuntu 22.04+
- Fedora 41+
- Debian 12+

# Officially Supported Hardware Wallets

- **Trezor**: Model T, Safe 3, Safe 5
- **ColdCard**: MK1, MK2, MK3, MK4, Q
- **Ledger**: Nano S, Nano S Plus, Nano X
- **Blockstream**: Jade
- **BitBox**: BitBox02-BtcOnly<sup>1*</sup>

<sup><sup>1*</sup> The device by default asks for a "Pairing code", currently, there is no such function in Wasabi. Therefore, either disable the feature or unlock the device with BitBoxApp or hwi-qt before using it with Wasabi.</sup>

# Officially Supported Architectures

- x64 (Windows, Linux, macOS)
- arm64 (macOS)

# FAQ

## What are the bottlenecks of officially supporting Operating Systems?

Wasabi dependencies are:
- .NET 8.0 [reqs](https://github.com/dotnet/core/blob/main/release-notes/8.0/supported-os.md).
- Avalonia [reqs](https://github.com/AvaloniaUI/Avalonia/wiki/Runtime-Requirements).
- NBitcoin dependencies and requirements are the same as .NET 8.0.

## What are the bottlenecks of officially supporting Hardware Wallets?

Wasabi dependencies are:
- [HWI](https://github.com/bitcoin-core/HWI), check the [device support](https://github.com/bitcoin-core/HWI#device-support) list there. Some hardware wallets supported by HWI are still not compatible with Wasabi Wallet because they implemented custom workflows.

## What about Tails and Whonix?

Tails and Whonix are privacy-oriented OSs, so it makes sense to use them with Wasabi Wallet. At the moment, Wasabi is working properly on these platforms, but our dependencies do not officially support them, so we cannot make promises regarding future stability.
To make Wasabi work on these OSs, it should be started with the following start up parameter: `--UseTor=EnabledOnlyRunning`.
