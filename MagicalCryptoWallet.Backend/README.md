# API Specification

## HTTP

  Requests and Responses are JSON.
  Requests have the following format: `/api/v1/{coin}/{controller}/`.
  Currently supported coins: `btc`.
  
  For example requesting fees: `GET /api/v1/btc/blockchain/fees`.

### Controller: Blockchain, Coin: BTC

|API | Description | Request | Response |
|--- | ---- | ---- | ---- |
|POST fees | Get fees based on Bitcoin Core's `estimatesmartfee` output. | ConfirmationTargets[] | ConfirmationTarget[] contains estimation mode and byte per satoshi pairs. Example: ![](https://i.imgur.com/Ggmif3R.png) |
|POST broadcast | Attempts to broadcast a transaction. | Hex |  |
|GET exchange-rates | Gets exchange rates for one Bitcoin. | Hex | ExchangeRates[] contains Ticker and ExchangeRate pairs. Example: ![](https://i.imgur.com/Id9cqxq.png) |
|POST filters | Gets block filters after the specified block hashes. If BlockHashes are not specified, the whole FilterTable is served. | (optional) BlockHashes[] | LastValidBlockHash, FilterTable[] contains BlockHash and Filter pairs. Example: ![](https://i.imgur.com/67Iswf5.png) |

### POST filters

  At initial syncronization the wallet must download the whole filter table by issuing `POST /api/v1/btc/blockchain/filters` request without specifying `BlockHashes`.  
  Filters are Golomb Rice filters of all the input and output native segregated witness `scriptPubKeys`. Thus wallets using this API can only handle `p2wpkh` scripts, therefore `p2pkh`, `p2sh`, `p2sh` over `p2wph` scripts are not supported. This restriction significantly lowers the size of the `FilterTable`, with that speeds up the wallet.
  The first filter is served from the block, where the first native segregated witness transaction happened ever.  
  When a client acquires a filter, it checks against its own keys and downloads the needed blocks from the Bitcoin P2P network, if needed. 
  
  In order to wallets properly be able to handle blockchain reorgs, wallets, those already acquired the initial filter table and wishes to be in sync must specify a `BlockHashes` array, where it specifies the last block hashes it acquired. The server then answers with the last valid block hash, and the consequent missing filter table.  
  Unless something unprecedentedly huge reorganization happens, for example User Activated Soft Fork, wallets may specify 6 to 100 block hashes.
