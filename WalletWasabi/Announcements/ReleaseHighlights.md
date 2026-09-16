## Release Highlights

#### 🌳 Taproot support for Payjoin
#### 🔐 Stricter security controls across the wallet
#### 🛡️ Enhanced coinjoin blame round protection

## Release Summary
Wasabi Wallet v2.8.3 adds Taproot support for Payjoin transactions, implements stricter security controls throughout the wallet, and strengthens verification of coinjoin blame rounds.

### Deprecation of compatibility password feature
Please create a new wallet if you see the following message while logging in to your wallet:
*Compatibility passphrase was used! Please consider generating a new wallet to ensure recoverability!*

Five years ago, because of a clipboard bug on macOS (OSX) in the Avalonia UI framework, users who created wallets by pasting complex passwords (with non-ASCII characters) got their password silently truncated and corrupted. Wallets created with the buggy passwords would be unrecoverable using standard BIP39 tools.

The mechanism for working around that issue will soon be removed.

### 🌳 Payjoin now supports Taproot addresses
Payjoin transactions now work with Taproot (P2TR) addresses. Payjoins are two-party collaborative transactions that improve fungibility.

### 🔐 Stricter security controls
Multiple security hardening measures have been implemented:
- Payjoin now requires Tor to be enabled for enhanced privacy
- Improved validation of compact block filters
- Better network diversity by connecting to nodes in different net groups
- Fixed value-conservation checks for Payjoin sender

### 🛡️ Enhanced coinjoin blame round protection
Improved coinjoin reliability and security:
- Clients now verify blame round inputs match the original round
- Fixed payment stalling when only non-private coins are temporarily banned
- Wallet no longer participates in blame rounds when it didn't sign in the original round

### 🎵 Music box always visible
The coinjoin "music box" is now always visible in the UI for easier access to coinjoin status and controls.

### 🐛 Bug fixes
- Fixed blockchain info exchange rate provider
- Improved handling of missing Windows startup registry key
