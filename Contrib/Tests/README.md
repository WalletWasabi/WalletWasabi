# Wasabi Wallet Regtest CoinJoin Testing Script

This bash script automates the setup and testing of Wasabi Wallet's CoinJoin functionality in a local Bitcoin regtest environment. It creates a complete testing environment with a Bitcoin node, Wasabi Coordinator, and multiple wallet clients performing simultaneous CoinJoins.

## Overview

The script performs the following operations:

1. Starts a Bitcoin Core node in regtest mode
2. Generates initial blocks to create spendable coins
3. Starts a Wasabi Coordinator
4. Starts a Wasabi Wallet client daemon
5. Creates multiple wallets and funds them
6. Initiates CoinJoin operations across all wallets
7. Monitors the coordinator logs for successful CoinJoin completion

## Prerequisites

## Configuration

The script uses the following default configuration (editable at the top of the script):

- `BITCOIN_DATADIR` - `/tmp/bitcoin-regtest` - Bitcoin regtest data directory
- `WASABI_DATADIR` - `/tmp/wasabi` - Wasabi data directory
- `BITCOIN_RPC_PORT` - `18443` - Bitcoin RPC port
- `BITCOIN_P2P_PORT` - `18444` - Bitcoin P2P port
- `COORDINATOR_PORT` - `37126` - Wasabi Coordinator port
- `WALLET_RPC_PORT` - `37128` - Wasabi Wallet RPC port
- `NUM_WALLETS` - `5` - Number of wallets to create
- `ADDRESSES_PER_WALLET` - `4` - Addresses per wallet to fund
- `TEST_TIMEOUT` - `600` - Timeout in seconds (10 minutes)

## Usage

```bash
# Make the script executable
chmod +x regtest-coinjoin-test.sh

# Run the script
./regtest-coinjoin-test.sh
```

## References

- Wasabi Wallet GitHub: https://github.com/zkSNACKs/WalletWasabi
- Bitcoin Core regtest documentation
- Wasabi RPC API documentation
```
