## Release Highlights
🪲 Bug Fixes  
💫 Settings Layout & UI Enhancements  
⤴️ Tor Update: Upgraded to v13.5.9  
🚫 Buy Anything Button Disabled  

## Release Summary
Wasabi Wallet v2.3.1 is a stability-focused release packed with critical bug fixes and preparatory changes for upcoming major features.

### 🪲 Notable Bug Fixes
- Fixed an issue where the transaction broadcaster displayed an error, while the transaction was successfully broadcast
- Resolved a problem with DNS endpoints for remote Bitcoin nodes
- Improved the amount decomposer to prevent privacy leaks when using the _payment-in-round_ feature
- Fixed sorting tables by amount
- Sometimes small rounds were not recognized by the wallet, leading to improper anon score computation

### 💫 Settings & UI Enhancements
The Settings layout has been refined, optimizing space for a more streamlined user experience.

Additionally, some new UI features have been implemented:
- Coin lists now display the address associated with each UTXO
- Added functionality to copy addresses directly from the Coin List view
- Consistent formatting of Fee Rate to 2 decimals without trailing 0

### 🚫 Buy Anything Button Disabled
This feature, launched nearly a year ago, allowed users to access the premium ShopinBit concierge service directly through the wallet.

This version disables it, for the following reasons:
- Limited usage among users
- The same service is fully accessible via the ShopInBit platform
- Occupies valuable interface space
- Improving the experience would require additional maintenance costs


Starting with this release, the button is removed, unless there are active or completed orders.

In the next release, the feature will be completely removed from the codebase.
