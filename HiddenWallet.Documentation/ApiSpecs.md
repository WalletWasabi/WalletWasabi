# Wallet

|API | Description    | Request body    | Response body   |
|--- | ---- | ---- | ---- |
|POST /api/v1/wallet/create  | Creates the wallet | password | mnemonic, creationTime |
|POST /api/v1/wallet/recover  | Recovers the wallet | password, mnemonic, creationTime | None |
|POST /api/v1/wallet/load | Loads the wallet and starts syncing | password  | None |
|GET /api/v1/wallet/wallet-exists | Checks if the wallet exists or not | None  | value |
|GET /api/v1/wallet/status | Displays dynamic information on the wallet | None  | walletState, headerHeight, trackingHeight, connectedNodeCount, memPoolTransactionCount, torState, isTumblerOnline, changeBump |
|GET /api/v1/wallet/shutdown | Gracefully shuts down the API | None  | None |
|GET /api/v1/wallet/balances/{account} | Displays the balances of the specified wallet account | None  | available, incoming |
|GET /api/v1/wallet/receive/{account} | Displays unused receive addresses of the specified wallet account | None  | addresses[], extPubKey |
|GET /api/v1/wallet/history/{account} | Displays the history of the specified wallet account | None  | history[] |
|POST /api/v1/wallet/build-transaction/{account} | Attempts to build a transaction with the specified wallet account | password, address, amount, feeType  | spendsUnconfirmed, fee, feePercentOfSent, hex, transaction |
|POST /api/v1/wallet/send-transaction | Broadcasts a transaction | hex  | None |
