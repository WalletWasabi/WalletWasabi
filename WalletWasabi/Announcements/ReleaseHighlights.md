## Release Highlights

#### ✅ Only use private funds for coinjoin payments
#### 🛡️ Copy URLs before opening browser
#### ⏪ Set gap limit in resync tool
#### 📊 Extended coinjoin transaction fee details
#### ⚙️ Bug fixes

## Release Summary
Wasabi Wallet v2.8.4 includes extra privacy features, UX improvements, and bug fixes.

### Deprecation of compatibility passphrase feature
Please create a new wallet if you see the following message while logging in to your wallet:
*Compatibility passphrase was used! Please consider generating a new wallet to ensure recoverability!*

Five years ago, because of a clipboard bug on macOS in the Avalonia UI framework, users who created wallets by pasting complex passwords (with non-ASCII characters) got their passphrase silently truncated and corrupted. Wallets created with the buggy passphrase would be unrecoverable using standard BIP39 tools. The mechanism for working around that issue will soon be removed.

### ✅ Only use private funds for coinjoin payments
A new option was added to the coinjoin settings to hold batched payments if any participating input is below the anonymity score target. This safeguard prevents nonprivate/semiprivate inputs and coinjoin payment outputs from appearing in the same round.

### 🛡️ Copy URLs before opening browser
Menu options that navigate to external links now ask before launching your default browser. Pasting the URL into Tor Browser helps protect your IP address like Wasabi does.

### ⏪ Set gap limit in resync tool
Bitcoin wallets can generate an unlimited number of new addresses. During wallet recovery, only a limited number of consecutive unused addresses are scanned before the software assumes all of the coins are found. If a gap between addresses that received coins exceeded that limit, funds might not be detected. The resync tool now supports increasing the gap limit to help recover funds that are buried under many unused addresses.

### 📊 Extended coinjoin transaction fee details
Coinjoin transaction details previously showed a single value for the fee paid to miners, dust that couldn't fit into private outputs, and outgoing payments. These individual costs are now broken down and easy to audit.

### ⚙️ Bug fixes
- Exchange rates and fee rates no longer get stuck
- Coinjoin payments now return from "in progress" to the queue and are attempted again once a previous signature is invalidated
- Synchronization now ignores block headers from forks
- Upgraded to Tor 0.4.9.13
