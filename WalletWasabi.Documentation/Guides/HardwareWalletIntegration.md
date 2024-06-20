# Hardware wallet integration into Wasabi 

## Introduction 

This is a technical document written for Hardware Wallet manufacturers. It describes the steps that need to be done before a hardware wallet can be supported by Wasabi. 
Wasabi does not directly support Hardware Wallets, but it uses [Bitcoin Core's Hardware Wallet Interface (HWI)](https://github.com/bitcoin-core/HWI) which is a command-line tool that unifies the commands for many devices. Wasabi includes the HWI binary and calls it every time whenever it interacts with the device. 

Wasabi's main priorities regarding hardware wallets:
- Compatibility - past, present, future
- Privacy
- Industry standardization

## Integration procedure

1. Integrate your hardware wallet into [HWI](https://github.com/bitcoin-core/HWI).
2. Add some tests into HWI, so compatibility issues can be caught at the early stages.
3. Test the compatibility with Wasabi. Import, Receive, Send, and Recover.
4. Write some [Kata tests](https://github.com/WalletWasabi/WalletWasabi/blob/master/WalletWasabi.Tests/AcceptanceTests/HwiKatas.cs) (manual tests), that can be used to detect compatibility issues when: Wasabi release, new firmware, etc.
5. Send at least one device (preferably two devices) to Wasabi HQ. To do so, contact us at `info@zksnacks.com`.
6. Wasabi team tests the device using the Kata tests and approves.
7. Create some content on how to use the device with our software, possibly in [the Wasabi documentation](https://github.com/zkSNACKs/WasabiDoc/blob/master/docs/using-wasabi/ColdWasabi.md).
8. Create a guide on how to initialize the device in offline mode - if possible. There should be a method to init the device without sharing the xpub.
9. During an initial testing period of at least half a year, Wasabi will unofficially support the hardware wallet - meaning that it is working but nothing is guaranteed. 
10. After a half-year grace period without compatibility problems or breaking changes, Wasabi will officially support this hardware wallet, meaning that Wasabi will give guidance related to the device and announce the [support of the device](https://github.com/WalletWasabi/WalletWasabi/blob/master/WalletWasabi.Documentation/WasabiCompatibility.md).
