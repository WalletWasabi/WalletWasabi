## Release Highlights
#### 💥 Support for Standard BIP 158 Block Filters<br/>
#### 🔐 Create & Recover SLIP 39 Shares<br/>
#### 💻 Full Node Integration Rework<br/>
#### 💪 Nostr Update Manager<br/>
#### 🤯 And more…<br/>

## Release Summary
Wasabi Wallet v2.6.0 - Prometheus continues to enhance Wasabi's resilience, implements SLIP 39 (Multi-Shares Backup), reworks the full node integration, etc...

### 💥 Support for Standard BIP 158 Block Filters
Wasabi’s client can now synchronize wallets using standard BIP 158 block filters, alongside the custom filters our backend provides.

This means the client can sync without our backend by fetching BIP 158 filters directly from a specified Bitcoin Node.

While further improvements are planned (like P2P filter fetching), this is a giant step for Wasabi’s resiliency, allowing clients to be fully sovereign and independent of specific servers.

### 🔐 Create & Recover SLIP 39 Shares

In addition to BIP 39, users can now create and recover wallets with multiple parts using SLIP 39.

You can specify the number of shares and the required threshold for recovery (e.g., a 2-of-3 scheme requires 2 of the 3 generated seed phrases to unlock the funds).

This offers much more flexibility for backups, as individual shares can be compromised without endangering funds.

While it sounds similar to multi-sig, this feature is purely cryptographic and uses Shamir Secret Sharing and has no on-chain footprint.

Special thanks to Trezor (SatoshiLabs) for sponsoring this amazing feature.

### 💻 Full Node Integration Rework
The previous, often frustrating integration with Bitcoin Knots (limited to that implementation, same machine, and non-standard) has been removed.

In its place, we've introduced a complete and standard integration with any Bitcoin Node implementation via the Bitcoin RPC Interface.

Simply enable the RPC server on your node and point Wasabi to it, ensuring all Bitcoin network interactions happen through your own node, bypassing third parties.

### 💪 Nostr Update Manager
Previously, Wasabi checked GitHub for new updates (an improvement over relying on our backend).

In this release, we're introducing a cutting-edge mechanism using the censorship-resistant Nostr network to receive update information and download locations.

This considerably improves resiliency, allowing updates even if GitHub is inaccessible. Naturally, the manager still verifies that displayed updates are signed by our secure certificate.

### 🤯 And more…
We've also been busy under the hood with several miscellaneous improvements:

- Updated Avalonia to v11.2.7, fixing numerous UI bugs (including restoring Minimize on macOS Sequoia!).
- Added a configurable third-party fallback for broadcasting transactions if other methods fail.
- Changed our Windows Code Signing Certificate, now using Azure Trusted Signing.
- Fixed numerous bugs, improved our codebase, and enhanced our CI pipeline.
- Provided the option to avoid using any third-party Exchange Rate and Fee Rate providers (Wasabi can work without them).
- Rebuilt all JSON Serialization mechanisms using standard dotnet converters.

### 🔮 A Glimpse on Tomorrow
This new version brings us closer to our ultimate goal: ensuring Wasabi is future-proof.

Our main focus areas for survival are:
- Ensuring users can always fully and securely use their client.
- Making contribution and forks easy through a codebase of the highest quality possible: understandable, maintainable, and improvable.

Simultaneously, we aim for Wasabi to remain a top-notch choice for self-custody Bitcoin wallets, bringing privacy without frustration.

As we achieve our survival goals, expect more cutting-edge improvements in Bitcoin privacy and self-custody.

Thank you for the trust you place in us by using Wasabi.

Stay tuned 👀
