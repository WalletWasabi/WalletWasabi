## Release Highlights

#### 🌳 Payjoin now supports Taproot addresses
#### 🔐 Stricter security controls across the wallet
#### 🛡️ Enhanced coinjoin blame round protection

## Release Summary
Wasabi Wallet v2.8.3 brings Taproot support to Payjoin transactions, implements stricter security controls throughout the wallet, and enhances coinjoin reliability with improved blame round handling.

### 🌳 Payjoin now supports Taproot addresses
Payjoin transactions now work with Taproot (P2TR) addresses, enabling more efficient and private collaborative transactions with lower fees and improved fungibility.

### 🔐 Stricter security controls
Multiple security hardening measures have been implemented:
- Payjoin now requires Tor to be enabled for enhanced privacy
- Improved validation of compact block filters
- Better network diversity by connecting to nodes in different net groups
- Fixed value-conservation checks for Payjoin sender

### 🛡️ Enhanced coinjoin blame round protection
Improved coinjoin reliability and security:
- Clients now verify blame round inputs before participating
- Fixed payment stalling when only non-private coins are temporarily banned
- Wallet no longer participates in blame rounds when it didn't sign in the original round

### 🎵 Music box always visible
The coinjoin music box is now always visible in the UI for easier access to coinjoin status and controls.

### 🐛 Bug fixes
- Fixed Blockstream exchange rate provider
- Improved handling of missing Windows startup registry key
