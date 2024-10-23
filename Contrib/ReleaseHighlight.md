## Release Highlights

🌐 Enhanced Tor integration
📊 Better BTC amount formatting
🔍 More insight on transactions
💰 [Beta] Functional Payment in Coinjoin (RPC)
🔒 Trezor Safe 5 & ColdCard Q support

## Release Summary

Wasabi Wallet v2.3.0.0 release

This version of Wasabi introduces major improvements to the user experience with a more intuitive and useful interface, while continuing our efforts to enhance and streamline the codebase for easier maintainability and better building blocks for further improvements.

🌐 Enhanced Tor integration

We’ve completely rewritten our Tor integration, replacing custom code with a more efficient, standard HttpClient. This not only improves performance but also enhances code clarity, making it easier to review which identities are used in specific contexts. This reduces potential errors and improves the wallet's overall security.

📊 Better BTC amount formatting

Amount formatting has been significantly improved across the wallet, offering clearer readability for both small and large balances. With immediate BTC-to-Sats conversion, it is easier to read balances, fees, Coinjoin outputs… The aesthetics of all screens presenting amounts are also been revisited to be more slick and consistent throughout the application.

🔍 More insight on transactions

Transaction previews now include detailed lists of inputs and outputs for both standard and Coinjoin transactions. This long-requested feature provides users with a deeper understanding of how Coinjoins enhance privacy and offers insight into the level of anonymity achieved with each transaction. Additionally, it is now visually clear how privacy suggestions improves transactions.

💰 [Beta] Payment in Coinjoin (RPC only)

Our Payment in Coinjoin feature is now officially in beta! It is currently accessible only though the RPC. While this feature is still in development and lacks certain functionalities, we encourage users to test it and provide feedback. Learn more about it in the [Documentation](https://docs.wasabiwallet.io/using-wasabi/RPC.html#payincoinjoin)

🔒 Trezor Safe 5 & ColdCard Q support

Hardware Wallet Interface (HWI) has been updated to version 3.1.0 along with support for Trezor Safe 5 & ColdCard Q.
