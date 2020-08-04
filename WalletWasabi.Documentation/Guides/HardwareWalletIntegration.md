Wasabi's main priorities regarding hardware wallets:
- Compatibility
- Privacy

1. Integrate your hardware wallet into HWI https://github.com/bitcoin-core/HWI
2. Add some tests into HWI, so compatibility problems can be caught at early stages
3. Test the compatibility with Wasabi. Import, Receive, Send, Recover.
4. Write some Kata tests (manual tests), that can be used to detect compatibility issues when: Wasabi release, new firmware, etc. https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Tests/AcceptanceTests/HwiKatas.cs
5. Send at least 1 device (preferable two devices) to Wasabi HQ, Contact us at info@zksnacks.com
6. Wasabi team tests the device using the Kata tests and approves.
7. Create some content on how to use the device with our software.
8. Create a guide on how to initialize the device in offline mode - if possible. There should be a method to init the device without sharing the XPUB.
9. Wasabi unofficially supports the hardware wallet - meaning that it is working but nothing is guaranteed. 
10. Half-year grace period - without compatibility problems or breaking changes
11. Wasabi officially supports the hardware wallet - meaning that Wasabi will give support related to the device and announce the support of the device. https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/WasabiCompatibility.md 


