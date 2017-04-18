|API | Description    | Request body    | Response body   |
|--- | ---- | ---- | ---- |
|POST /api/v1/wallet/create  | Creates the wallet | password | mnemonic |
|POST /api/v1/wallet/recover  | Recovers the wallet | password, mnemonic, creationTime | None |
|POST /api/v1/wallet/load | Loads the wallet and starts syncing | password  | None |
