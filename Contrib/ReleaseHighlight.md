## Release Highlights

🥕 Taproot receive addresses
🚀 Faster transaction broadcasting
🔍 Help to setup and find a coordinator
⏫ More accurate CPFP fee estimation
📉 Safer protocol
🪲 Backend optimizations

## Release Summary

Wasabi Wallet v2.2.0.0 release

This version of Wasabi is full of extra features, improvements, and fixes. The highlights are:

🥕 Taproot receive addresses

Users can now choose taproot when generating a new receiving address. Spending a taproot input is 16% cheaper compared to spending a native segwit input. Users aren’t the only ones that benefit - Bitcoin nodes can verify taproot’s signatures faster than legacy ones, so the entire network gets a little speed boost.

🚀 Faster transaction broadcasting

A more sophisticated transaction broadcasting mechanism was introduced to make the process faster. Transactions are now broadcast to multiple nodes in parallel (through the Tor network) and fail more quickly if the transaction is rejected by the network. Users who upgrade will notice it now takes half as long to send their coins.

🔍 Help to setup and find a coordinator

A new message is now shown when no coordinator is configured, along with some help to understand how to find and setup one.

⏫ More accurate CPFP fee estimation

The CPFP feature now takes into account the fee paid by the parent transaction. In previous releases, the child transaction assumed the parent paid 0 sats in fees, and thus always overpaid for the speed up.. This improvement will be especially significant in high-fee environments, where the CPFP feature is most useful!

📉 Safer protocol

In order to avoid introducing unknown incentives and to limit risk to users, the coordination fee concept has been removed. Only coinjoin coordinators that don't charge any coordination fee continue to be supported.

🪲 Backend optimizations

 Users who run a Wasabi backend can now do so with a pruned node and significant CPU/RAM savings. This makes hosting a backend much cheaper and helps to decentralize this component which Wasabi clients still depend on.
