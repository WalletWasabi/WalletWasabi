## Release Highlights
#### 🟠 Enhanced Bitcoin Node Integration
#### 🎨 Refreshed UI with Icons & Animations
#### ⚙️ Dedicated Config Files Per Network
#### 🤯 Stronger & Smarter Coordinators
#### 🛠️ Refinements & Fixes

## Release Summary
Wasabi Wallet v2.7.0 is a stabilization release that not only strengthens reliability but also brings a fresh look and smoother performance.

### 🟠 Enhanced Bitcoin Node Integration
Bitcoin node connectivity is now more seamless. RPC endpoint handling has been refined for smoother setup, with support for onion-service RPC interfaces.

Additionally, Wasabi no longer bundles bitcoind binaries, while block downloading has been simplified and made more dependable.

### 🎨 Refreshed UI with Icons & Animations
The interface has been given a polished update. Subtle animations and a balanced color scheme breathe new life into Wasabi’s design, making it both cleaner and less aggressive.

### ⚙️ Dedicated Config Files Per Network
Each network  Mainnet, Testnet4, and Regtest — now has its own independent configuration file. Switching to test networks is easier and your preferences are always preserved.

### 🤯 Stronger & Smarter Coordinators
Coordinators are automatically published as onion services right out of the box: no manual Tor setup needed. Coordinators can now also run on pruned nodes in blocksonly mode.

Plus, fallback fee rate providers were implemented (mempool.space and blockstream.info), ensuring accurate fee estimates, even if your node can’t provide them.

### 🛠️ Refinements & Fixes
- **Full-RBF by default** – All transactions are treated as replaceable.
- **Resilient HTTP communication** – Smarter retry handling makes connections sturdier.
- **Seed recovery fixes** – Annoying typing issues are resolved.
- **Sharper fee estimations** – Precise decimal calculations with no rounding loss.
- **NBitcoin updated to 8.0.14** – Latest Bitcoin protocol improvements included.
- **Clearer terminology** – “Backend” is now called “Indexer.”
- **Lean codebase** – Legacy components like TurboSync and BlockNotifier removed.
- **Safer Coinjoin handling** – Excluded Coins can only be changed when Coinjoin is paused.
- **Donation Button removed from Main Screen** – The button is gone, but donations are still possible via the search bar.
- **Conflux by default** - Better Tor configuration for improved connectivity.
