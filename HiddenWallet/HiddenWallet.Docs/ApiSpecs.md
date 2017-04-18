|API | Description    | Request body    | Response body   |
|--- | ---- | ---- | ---- |
|POST /api/v1/wallet/create  | Creates the wallet | Password | Mnemonic |
|POST /api/v1/wallet/recover  | Recovers the wallet | Password, Mnemonic, CreationTime | None |
|GET /api/v1/wallet/load | Loads the wallet and starts syncing | Password  | None |
