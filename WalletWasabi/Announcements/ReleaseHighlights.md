## Release Highlights
#### 🤯 Coordinators harder to censor
#### 🟠 Enhanced Bitcoin Node Integration
#### 🎨 Fresh UI with Colorful Icons & Animations
#### ⚙️ One Config File per Network
#### 🛠️️ Miscellaneous Improvements & Fixes

## Release Summary

Wasabi Wallet v2.7.0 is a stabilisation release that delivers a refreshed interface alongside plenty of bug-fixes and important architectural improvements.

### 🤯 Coordinators harder to censor

Coordinators are published as onion services automatically making them available immediately after installation. Operators don't need to install Tor or
configure it. Additionally, a coordinator can now be run with an automatically pruned node even in `blocksonly` mode.

Two fallback fee rate providers `mempool.space` and `blockstream.info` were added to be used in case the Bitcoin node cannot provide estimations. Those are
queried through Tor in case the coordinator is only available as an onion service.

### 🟠 Enhanced Bitcoin Node Integration

RPC endpoint handling has been improved for easier Bitcoin node connectivity, allowing also to connect to RPC interfaces available as onion services.
Additionally, Wasabi no longer ships with bitcoind binaries, and block downloading has been simplified and made more reliable.

### 🎨 Fresh UI with Colorful Icons & Animations

Visual refresh with a refined and less aggressive color scheme.

### ⚙️ One Config File per Network

Mainnet, Testnet4, and Regtest now have independent configuration files, making it easier to save preferences and switch to test networks.

### 🛠️️ Miscellaneous Improvements & Fixes
- **Full-RBF** - RBF flag is no longer considered as all transactions are now treated as replaceable by default. Wasabi transactions no longer signal for replaceability.
- **More reliable Http communication** - implements a retry strategy across all the http communication.
- **Fixes in recover by seed** - fixed some annoying typing issues in seed phrase recovery process
- **More precise fee rate estimations and calculation** - do not round up or down and consider decimal positions always.
- **Updated NBitcoin to 8.0.14** - latest Bitcoin protocol library with bug fixes and improvements
- **Improved terminology** - "Backend" renamed to "Indexer" throughout the UI for clarity
- **Removed legacy components** - remove TurboSync, BlockNotifier, and other deprecated features to improve codebase maintainability
- **Force to stop Coinjoin before changing Excluded Coins** - the Excluded Coins can now only be changed when Coinjoin is stopped, preventing potential issues
- **Remove the Donation button** - donations have diminished at the point that it isn't worth ruining the UI with it.
