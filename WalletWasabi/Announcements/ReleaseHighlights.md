## Release Highlights
#### ⚙️ Custom indexer replaced with standard Bitcoin RPC
#### 🚀 Pay in coinjoin
#### ↘️ Sub-1 sat/vByte transaction fees
#### ♻️ Payment batching
#### 📎 Support for arm64 Linux, Tails, and Whonix
#### 📜 Scheme scripting language
#### 🔑 Signet test network

## Release Summary
Wasabi Wallet v2.8.0 is a massive release with faster performance, improved privacy, lower fees, and expanded device support.

### ⚙️ Custom indexer replaced with standard Bitcoin RPC
Wasabi pioneered the use of compact block filters for private wallet synchronization. This involved implementing a custom indexer to build the filters, and hosting a server to provide them to clients.

Progress continued on block filters, resulting in standardized BIPs and direct support in Bitcoin node software. This release discards the legacy indexer and exclusively uses the node RPC for synchronizing block filters. In addition to making Wasabi's architecture simpler and more flexible, this massive refactoring also speeds up sync performance by up to 5x.

Onboarding new users to Wasabi is now instant thanks to wallet birthday checkpoints. Newly generated wallets no longer waste time and bandwidth downloading old blockchain history since it would be impossible for a new wallet to have previously received any transactions.

### 🚀 Pay in coinjoin
Sending payments directly inside a coinjoin transaction uses block space more efficiently and improves privacy in several ways:

- The age of your inputs is not revealed, so the receiver does not learn how long you’ve held your coins. 
- The size of your change is not revealed, so the receiver does not learn the amount of coins you have left over. 
- You can batch multiple payments into one transaction without revealing they originate from the same sender.

### ↘️ Sub-1 sat/vByte transaction fees
You can now spend coins using fee rates as low as 0.1 sat/vByte, letting you save up to 90% on mining fees. If a low fee transaction gets “stuck”, you can use Replace By Fee (RBF) to speed it up.

### ♻️ Payment batching
You can now add multiple outputs to a transaction and save even more on fees. This significantly reduces the amount of block space used compared to sending multiple payments individually.

### 📎 Support for arm64 Linux, Tails, and Whonix
Linux users with arm64 devices are now part of the Wasabi family. Tails and Whonix installations are now automatic and no longer require manual Tor configuration.

### 📜 Scheme scripting language
Needs description

### 🔑 Signet test network
Testnet3 and Testnet4 use proof of work for generating blocks, just like mainnet. Because testnet coins have no value, low mining difficulty allows an attacker with a small amount of hashpower to flood blocks or create long reorgs.

Signet is another test network that allows a set of signers to create blocks. This reduces the unpredictable behavior so developers can work with a stable environment. Before switching networks, open the signet config file to connect Wasabi to your signet node.
