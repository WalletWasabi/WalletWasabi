## Release Highlights
#### 💪 3rd Party Providers for Fee & Exchange Rate<br/>
#### 👨‍🔧 Quality of Life Features<br/>
#### 📦 Backend and Coordinator packaged for Linux<br/>

## Release Summary
Wasabi Wallet v2.5.0 continues to enhance Wasabi's resilience and improve the user interface to reduce frustration.

### 💪 3rd Party Providers for Fee & Exchange Rate

Fee rate estimations and exchange rate providers are now configurable.

Previously, this information was provided by Wasabi's backend. This change aligns with our long-term goal of completely removing dependence on the backend.

After this release, Wasabi will be significantly closer to achieving that goal, with most basic wallet features functioning without an active backend connection. You can read more about the remaining steps [here](https://github.com/orgs/WalletWasabi/discussions/13661).

### 👨‍🔧 Quality of Life Features

In the last version, we introduced a donation button to gauge community interest in funding Wasabi's future. We have been overwhelmed by the support and interest shown by our community, including long-time users and privacy enthusiasts.

We want to thank you for believing in Wasabi and for your support, whether through using the software, providing feedback, contributing financially, or contributing to the open-source project in any form.

To show our gratitude, we've designed this release for you, our community. We've finally addressed some of the oldest bugs and feature requests that have been long-awaited!

Listing all the improvements would be too lengthy, but here are some examples:
- More & better-designed settings (specifically for coinjoin)
- Randomly skipping rounds was removed
- Change avoidance suggestions are now more precise

We expect these improvements to significantly enhance the overall experience of using Wasabi as your daily Bitcoin wallet.

### 📦 Backend and Coordinator packaged for Linux

The Debian package now includes two extra binaries: one for the backend (Wallet API) and one for the coordinator (Coinjoin API). This update simplifies the deployment process for Linux users, making it straightforward to set up their backend servers and coordinators.

This makes it easier for community members to run a backend and/or a coordinator, private or publicly accessible, which lowers the barrier to contributing to the resiliency of Wasabi's infrastructure against potential attacks and technical failures.
