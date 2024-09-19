## Release Highlights

🥕 Taproot receive addresses
🚀 Faster transaction broadcasting
⏫ More accurate CPFP fee estimation
📉 Safer protocol
🪲 Backend optimizations

## Release Summary

Wasabi Wallet v2.2.0.0 release

This version of Wasabi is full of extra features, improvements, and fixes. Here’s what got updated in today’s release:

🥕 Taproot receive addresses

Users can now choose taproot when generating a new receiving address. Spending a taproot input is 16% cheaper compared to spending a native segwit input. Users aren’t the only ones that benefit - Bitcoin nodes can verify taproot’s signatures faster than legacy ones, so the entire network gets a little speed boost.

🚀 Faster transaction broadcasting

Spending your coins is not only cheaper in this release, it’s also faster! Transactions are now broadcast to multiple nodes in parallel (through the Tor network) and fail more quickly if the transaction is invalid. Users who upgrade will notice it now takes half as long to send their coins.

⏫ More accurate CPFP fee estimation

The CPFP feature now takes into account the fee paid by the parent transaction. In previous releases, the child transaction assumed the parent paid 0 sats in fees. This improvement will be especially significant in high-fee environments, where the CPFP feature is most useful!

📉 Safer protocol

In order to avoid introducing unknown incentives and to limit risk to users, the coordination fee concept has been removed. Free to use coinjoin coordinators are supported.

🪲 Backend optimizations

 Users who run a Wasabi backend can now do so with a pruned node and significant CPU/RAM savings. This makes hosting a backend much cheaper and helps to decentralize this component which Wasabi clients still depend on.
