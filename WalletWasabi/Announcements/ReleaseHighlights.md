## Release Highlights
#### 🟠 Enhanced Bitcoin Node Integration
#### 🎨 Fresh UI with Colorful Icons & Animations
#### ⚙️ One Config File per Network
#### 🛠️️ Miscellaneous Improvements & Fixes

## Release Summary

Wasabi Wallet v2.7.0 is a stabilisation release that delivers a refreshed interface alongside plenty of bug-fixes and important architectural improvements.

### 🟠 Enhanced Bitcoin Node Integration

RPC endpoint handling has been improved for easier Bitcoin node connectivity. Additionally, Wasabi no longer ships with bitcoind binaries, and Block downloading has been simplified and made more reliable.

### 🎨 Fresh UI with Colorful Icons & Animations

Visual refresh with a refined and less-aggressive color scheme.

### ⚙️ One Config File per Network

Mainnet, Testnet, and Regtest now have independent configuration files, making it easier to save preferences and switch to test networks.

### 🛠️️ Miscellaneous Improvements & Fixes
- **Go full with full-rbf** - RBF flag is no longer considered as all transactions are now treated as replaceable by default, aligning with modern Bitcoin Core practices
- **Updated NBitcoin to 8.0.14** - latest Bitcoin protocol library with bug fixes and improvements
- **Improved terminology** - "Backend" renamed to "Indexer" throughout the UI for clarity
- **Fixes in recover by seed** - fixed some annoying typing issues in seed phrase recovery process
- **Removed legacy components** - cleaned up TurboSync, BlockNotifier, and other deprecated features to improve codebase maintainability
- **Enhanced error handling** - improved network error recovery
- **Force to stop Coinjoin before changing Excluded Coins** - the Excluded Coins can now only be changed when Coinjoin is stopped, preventing potential issues
