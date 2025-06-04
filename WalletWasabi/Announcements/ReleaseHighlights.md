## Release Highlights
#### 💥 Support for Standard BIP 158 Block Filters
#### 💻 Full Node Integration Rework
#### 🔐 Create & Recover SLIP 39 Shares
#### 💪 Nostr Update Manager
#### 🤯 And more…

## Release Summary

Wasabi Wallet v2.6.0 "Prometheus" marks a significant milestone in our survival strategy, delivering major improvements in resiliency by eliminating dependency on centralized infrastructure while making it harder to stop.

### 💥 Support for Standard BIP 158 Block Filters

Wasabi can now synchronize using BIP 158 filters without requiring a indexer/indexer. You can connect directly to your own node, significantly enhancing synchronization speed and resilience. This improvement allows clients to operate fully sovereign and independent of specific servers.

### 💻 Full Node Integration Rework

The previous integration was replaced with a simpler, more flexible system which is not limited to a specific Bitcoin node fork and doesn't depend on the node running on the same machine as Wasabi, or require modifications to the node's configuration.

Simply enable the RPC server on your node and point Wasabi to it, ensuring all Bitcoin network interactions happen through your own node, bypassing third parties for getting blocks, fee estimations, block filters, and broadcasting transactions.

### 🔐 Create & Recover SLIP 39 Shares

You can now create and recover wallets with multiple share backups using SLIP 39. Simply specify the number of shares and the required threshold for recovery (e.g., a 2-of-3 scheme requires 2 of the 3 generated seed phrases to unlock the funds).

This offers additional flexibility for backups, as individual shares can be compromised without endangering funds.

Special thanks to Trezor (SatoshiLabs) for sponsoring this amazing feature.

### 💪 Nostr Update Manager

We're introducing a cutting-edge mechanism using the censorship-resistant Nostr network to receive update information and download locations instead of relying on GitHub's goodwill.

This considerably improves resiliency, allowing updates even if GitHub is inaccessible. Naturally, the manager still verifies that displayed updates are signed by our secure certificate.

### 🤯 And more…
We've also been busy under the hood with several miscellaneous improvements:

- Updated Avalonia to v11.2.7, fixing numerous UI bugs (including restoring Minimize on macOS Sequoia!).
- Added a configurable third-party fallback for broadcasting transactions if other methods fail.
- Changed our Windows Code Signing Certificate, now using Azure Trusted Signing.
- Fixed numerous bugs, improved our codebase, and enhanced our CI pipeline.
- Provided the option to avoid using any third-party Exchange Rate and Fee Rate providers (Wasabi can work without them).
- Rebuilt all JSON Serialization mechanisms avoiding default .NET converters. Serialization is now stricter.

### 🔮 A Glimpse of Tomorrow
This new version brings us closer to our ultimate goal: ensuring Wasabi is future-proof.

Our main focus areas for survival are:
- Ensuring users can always fully and securely use their client.
- Making contribution and forks easy through a codebase of the highest quality possible: understandable, maintainable, and improvable.

Simultaneously, we aim for Wasabi to remain a top-notch choice for self-custody Bitcoin wallets, bringing privacy without frustration.

As we achieve our survival goals, expect more cutting-edge improvements in Bitcoin privacy and self-custody.

Thank you for the trust you place in us by using Wasabi.

Stay tuned 👀
