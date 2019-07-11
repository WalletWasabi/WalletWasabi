# API Specification

**ATTENTION:** This document describes the initial specification. The actual implementation may significantly differ. You can find up to date documentation here:  
- TestNet: http://testwnp3fugjln6vh5vpj7mvq3lkqqwjj3c2aafyu7laxz42kgwh2rad.onion/swagger
- Main: http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion/swagger

## Related Documents:

[Backend Deployment And Update Instructions](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/BackendDeployment.md)

## HTTP

  Requests and Responses are JSON.
  Requests have the following format: `/api/v3/{coin}/{controller}/`.
  Currently supported coins: `btc`.
  
  For example requesting fees: `GET /api/v3/btc/offchain/exchange-rates`.

### Controller: Blockchain, Coin: BTC

| API | Description | Request | Response |
| --- | ---- | ---- | ---- |
| GET fees?{comma separated confirmationTargets} | Gets fees for the requested confirmation targets based on Bitcoin Core's `estimatesmartfee` output. |  | ConfirmationTarget[] contains estimation mode and byte per satoshi pairs. Example: ![](https://i.imgur.com/Ggmif3R.png) |
| POST broadcast | Attempts to broadcast a transaction. | Hex |  |
| GET exchange-rates | Gets exchange rates for one Bitcoin. |  | ExchangeRates[] contains Ticker and ExchangeRate pairs. Example: ![](https://i.imgur.com/Id9cqxq.png) |
| GET filters/{blockHash} | Gets block filters from the specified block hash. |  | An array of blockHash : filter pairs. |

#### POST filters

  At the initial synchronization the wallet must specify the hash of the first block that contains native segwit output. This hash must be hard coded into the client.  
  - First block with P2WPKH: dfcec48bb8491856c353306ab5febeb7e99e4d783eedf3de98f3ee0812b92bad
  - First block with P2WPKH on TestNet: b29fbe96bf737000f8e3536e9b4681a01b1ca6be3ac4bd1f8269cdbd465e6700
  
  Filters are Golomb Rice filters of all the input and output native segregated witness `scriptPubKeys`. Thus wallets using this API can only handle `p2wpkh` scripts, therefore `p2pkh`, `p2sh`, `p2sh` over `p2wph` scripts are not supported. This restriction significantly lowers the size of the `FilterTable`, with that speeds up the wallet.
  When a client acquires a filter, it checks against its own keys and downloads the needed blocks from the Bitcoin P2P network, if needed. 
  
#### Handling Reorgs

  If the answer to the `filters` request is not found, then the client steps back one block and queries the filters with that previous hash. This can happen multiple times. This will only happen when blockchain reorganization has occurred. 

### Controller: ChaumianCoinJoin, Coin: BTC

| API | Description | Request | Response |
| --- | ---- | ---- | ---- |
| GET status | Satoshi gets various status information. | | CurrentPhase, Denomination, RegisteredPeerCount, RequiredPeerCount, ForcedRoundStartMinutesLeft, MaximumInputCountPerPeer, FeePerInputs, FeePerOutputs, CoordinatorFee, Version |
| POST inputs | Alice registers her inputs. | Inputs[(Input, Proof)], BlindedOutputHex, ChangeOutputs[] | SignedBlindedOutput, UniqueId |
| POST confirmation | Alice must confirm her participation periodically in InputRegistration phase and confirm once in ConnectionConfirmation phase. | UniqueId | Phase |
| POST unconfirmation | Alice can revoke her registration without penalty if the current phase is InputRegistration. | UniqueId | |
| POST outputs | Bob registers his output. | Output, Signature, RoundId | |
| GET coinjoin | Alice asks for the final CoinJoin transaction. | UniqueId | Transaction |
| POST signatures | Alice posts her partial signatures. | UniqueId, Signatures[(Witness, Index)] | |
