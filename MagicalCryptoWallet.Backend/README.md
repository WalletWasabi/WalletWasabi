# API Specification

## HTTP

  Requests and Responses are JSON.
  Requests have the following format: `/api/v1/{coin}/{controller}/`.
  Currently supported coins: `btc`.
  
  For example requesting fees: `GET /api/v1/btc/blockchain/exchange-rates`.

### Controller: Blockchain, Coin: BTC

|API | Description | Request | Response |
|--- | ---- | ---- | ---- |
|GET fees?{comma separated confirmationTargets} | Get fees for the requested confirmation targets based on Bitcoin Core's `estimatesmartfee` output. |  | ConfirmationTarget[] contains estimation mode and byte per satoshi pairs. Example: ![](https://i.imgur.com/Ggmif3R.png) |
|POST broadcast | Attempts to broadcast a transaction. | Hex |  |
|GET exchange-rates | Gets exchange rates for one Bitcoin. |  | ExchangeRates[] contains Ticker and ExchangeRate pairs. Example: ![](https://i.imgur.com/Id9cqxq.png) |
|GET filters/{blockHash} | Gets block filters from the specified block hash. |  | An array of blockHash : filter pairs. |

### POST filters

  At initial syncronization the wallet must specify the hash of the first block that contains native segwit output. This hash must be hard coded into the client. (ToDo: find the hash.)  
  Filters are Golomb Rice filters of all the input and output native segregated witness `scriptPubKeys`. Thus wallets using this API can only handle `p2wpkh` scripts, therefore `p2pkh`, `p2sh`, `p2sh` over `p2wph` scripts are not supported. This restriction significantly lowers the size of the `FilterTable`, with that speeds up the wallet.
  When a client acquires a filter, it checks against its own keys and downloads the needed blocks from the Bitcoin P2P network, if needed. 
  
#### Handling Reorgs

  If the answer to the `filters` request is not found, then the client steps back one block and queries the filters with that previous hash. This can happen multiple times. This will only happen when blockchain reorganization happened. 
