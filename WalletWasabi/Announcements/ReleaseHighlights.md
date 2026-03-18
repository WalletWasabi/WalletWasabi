## Release Highlights
#### 💰 Sub-1 sat/vbyte transaction fees
#### 🛡️ Batch payments inside coinjoin transactions
#### 📜 Scheme scripting language
#### 🔑 Signet test network
#### 📎 Dependencies upgraded
#### ♻️ Updated default experience
#### 🔎 Bug fixes

## Release Summary
Wasabi Wallet v2.8.0 is a massive release with several new features and many subtle improvements.

### 💰 Sub-1 sat/vbyte transaction fees
You can now spend coins using fee rates as low as 0.1 sat/vByte, letting you save up to 90% on mining fees. If a low fee transaction gets “stuck”, you can use Replace By Fee (RBF) to speed it up.

### 🛡️ GUI support for batching payments inside coinjoin transactions
Sending payments directly inside a coinjoin transaction uses block space more efficiently and improves privacy in several ways:

- The age of your inputs is not revealed, so the receiver does not learn how long you’ve held your coins. 
- The size of your change is not revealed, so the receiver does not learn the amount of coins you have left over. 
- You can batch multiple payments into one transaction without revealing they originate from the same sender.

### 📜 Scheme scripting language
Needs description

### 🔑 Signet support
Testnet3 and Testnet4 use proof of work for generating blocks, just like mainnet. Because testnet coins have no value, low mining difficulty allows an attacker with a small amount of hashpower to flood blocks or create long reorgs.

Signet is another test network that allows a set of signers to create blocks. This reduces the unpredictable behavior so developers can work with a stable environment. Wasabi does not ship with a default signet indexer, so you must specify one in the signet config file or connect it to your signet node.

### 📎 Dependencies upgraded
Continuous maintenance is done to keep Wasabi secure and compatible with new devices. Under the hood, here are the parts that were upgraded:

- .NET 8 -> .NET 10
- Tor 0.4.8.13 -> Tor 0.4.8.21
- Avalonia 11.2.7 -> Avalonia 11.3.11
- xUnit 2.6.6 -> xUnit 2.9.3

### ♻️ Updated default experience
Whonix and Tails operating systems now configure Tor connections automatically. SLIP39 multi-share backups are now listed ahead of BIP39 recovery word backups. The default minimum and maximum values in the coinjoin settings were changed to better fit the current market and fee environment.

### 🔎 Bug fixes
An issue with sending to BIP352 silent payment addresses was fixed. Hundreds of build warnings were fixed. Software verification and security patches are included thanks to Ledger’s Donjon team.
