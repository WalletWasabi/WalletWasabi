# API Specification

## HTTP

Requests and Responses are in JSON.

### Controller: Blockchain, Coin: btc

| API                                                                            | Description                                               | Request | Response |
|--------------------------------------------------------------------------------|-----------------------------------------------------------| ---- | ---- |
| GET /api/v4/btc/blockchain/filters?bestKnownBlockHash={blockHash}&count={count} | Gets `count` block filters from the specified block hash. | `bestKnownBlockHash` (string): The known block hash. `count` (int): Number of filters to retrieve. | `FiltersResponse` object containing `bestHeight` and array of filters. |
| GET /api/software/versions                                                     | Gets current API version | | `VersionsResponse` object containing backend version information. |

#### GET /api/v4/btc/blockchain/filters

**Parameters:**

- `bestKnownBlockHash` (string, required): The hash of the known block from which to start retrieving filters.
- `count` (int, required): The number of filters to retrieve. Must be greater than 0.

**Responses:**

- **200 OK**: Returns a `FiltersResponse` containing:
  - `bestHeight` (int): The best height of the filters.
  - `Filters` (array): An array of blockHash : filter pairs.

- **204 No Content**: No filters are available from the specified block hash.

- **400 Bad Request**: Invalid block hash or count is provided (count must be greater than 0).

- **404 Not Found**: The provided `bestKnownBlockHash` is not found.

**Description:**

At the initial synchronization the wallet must specify the hash of the first block that contains native segwit output, both for SegWit and Taproot. This hash must be hard coded into the client.

- First SegWit block with P2WPKH: 0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893
- First SegWit block with P2WPKH on TestNet4: 00000000da84f2bafbbc53dee25a72ae507ff4914b867c565be350b0da8bf043
- First Taproot block: 0000000000000000000687bca986194dc2c1f949318629b44bb54ec0a94d8244
- First Taproot block TestNet4: 00000000da84f2bafbbc53dee25a72ae507ff4914b867c565be350b0da8bf043

Filters are Golomb Rice filters of all the input and output native segregated witness `scriptPubKeys`. Thus wallets using this API can only handle `P2WPKH` and `P2TR` scripts, therefore `P2PKH`, `P2SH`, and `P2WPKH` over `P2SH` scripts are not supported. This restriction significantly lowers the size of the `FilterTable`, which speeds up the wallet filter synchronization.

When a client acquires a filter, it checks against its own keys and downloads the needed blocks from the Bitcoin P2P network, if needed.

#### Handling Reorgs

If the answer to the `filters` request returns 404 Not Found, then the client steps back one block and queries the filters with that previous hash. This can happen multiple times. This will only happen when blockchain reorganization has occurred.

#### GET /api/software/versions

**Responses:**

- **200 OK**: Returns a `VersionsResponse` object containing the current backend version information.
