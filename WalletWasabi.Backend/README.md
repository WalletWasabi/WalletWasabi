# API Specification

## HTTP

  Requests and Responses are in JSON.
  Requests have the following format: `/api/v4/{coin}/{controller}/`.
  Currently supported coins: `btc`.

### Controller: Blockchain, Coin: btc

| API | Description | Request | Response |
| --- | ---- | ---- | ---- |
| POST broadcast | Attempts to broadcast a transaction. | Hex |  |
| GET filters/{blockHash} | Gets block filters from the specified block hash. |  | An array of blockHash : filter pairs. |
| GET status | Gets current status of filter and coinjoin creation. |  |  |

#### POST filters

  At the initial synchronization the wallet must specify the hash of the first block that contains native segwit output, both for SegWit and Taproot. This hash must be hard coded into the client.
  - First SegWit block with P2WPKH: 0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893
  - First SegWit block with P2WPKH on TestNet: 00000000000f0d5edcaeba823db17f366be49a80d91d15b77747c2e017b8c20a
  - First Taproot block: 0000000000000000000687bca986194dc2c1f949318629b44bb54ec0a94d8244
  - First Taproot block TestNet: 00000000000000216dc4eb2bd27764891ec0c961b0da7562fe63678e164d62a0

  Filters are Golomb Rice filters of all the input and output native segregated witness `scriptPubKeys`. Thus wallets using this API can only handle `P2WPKH` and `P2TR` scripts, therefore `P2PKH`, `P2SH`, and `P2WPKH` over `P2SH` scripts are not supported. This restriction significantly lowers the size of the `FilterTable`, which speeds up the wallet filter synchronization.
  When a client acquires a filter, it checks against its own keys and downloads the needed blocks from the Bitcoin P2P network, if needed.

#### Handling Reorgs

  If the answer to the `filters` request is not found, then the client steps back one block and queries the filters with that previous hash. This can happen multiple times. This will only happen when blockchain reorganization has occurred.

