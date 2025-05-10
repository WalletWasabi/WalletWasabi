Wasabi Daemon
=============

Wasabi daemon is a _headless_ Wasabi wallet designed to minimize the usage of resources (CPU, GPU, Memory, Bandwidth) with the goal of
making it more suitable for running all the time in the background.

## Configuration

All configuration options available via `Config.json` file are also available as command line arguments and environment variables:

### Command Line and Environment variables

* Command line switches have the form `--switch_name=value` where _switch_name_ is the same name that is used in the config file (case insensitive).
* Environment variables have the form `WASABI_SWITCHNAME` where _SWITCHNAME_ is the same name that is used in the config file.

A few examples:

| Config file                | Command line                | Environment variable             |
|----------------------------|-----------------------------|----------------------------------|
| Network: "TestNet"         | --network=testnet           | WASABI_NETWORK=testnet           |
| JsonRpcServerEnabled: true | --jsonrpcserverenabled=true | WASABI_JSONRPCSERVERENABLED=true |
| UseTor: true               | --usetor=true               | WASABI_USETOR=true               |
| DustThreshold: "0.00005"   | --dustthreshold=0.00005     | WASABI_DUSTTHRESHOLD=0.00005     |

### Values precedence

* **Values passed by command line arguments** have the highest precedence and override values in environment variables and those specified in config files.
* **Values stored in environment variables** have higher precedence than those in config file and lower precedence than the ones pass by command line.
* **Values stored in config file** have the lower precedence.

### Special values

There are a few special switches that are not present in the `Config.json` file and are only available using command line and/or variable environment:

* **LogLevel** to specify the level of detail used during logging
* **DataDir** to specify the path to the directory used during runtime.
* **BlockOnly** to instruct wasabi to ignore p2p transactions
* **Wallet** to instruct wasabi to open a wallet automatically after started.

### Examples

Run Wasabi and connect to the testnet Bitcoin network with Tor disabled and accept JSON RPC calls. Store everything in `$HOME/temp/wasabi-1`.

```bash
$ wasabi.daemon --usetor=false --datadir="$HOME/temp/wasabi-1" --network=testnet --jsonrpcserverenabled=true --blockonly=true
```

Run Wasabi Daemon and connect to the testnet Bitcoin network.

```bash
$ WASABI_NETWORK=testnet wasabi.daemon
```

Run Wasabi and open two wallets: AliceWallet and BobWallet

```bash
$ wasabi.daemon --wallet=AliceWallet --wallet=BobWallet
```

### Version

```bash
$ wasabi.daemon --version
Wasabi Daemon 2.0.3.0
```

### Usage

To interact with the daemon, use the [RPC server](https://docs.wasabiwallet.io/using-wasabi/RPC.html) or the [wcli script](https://github.com/WalletWasabi/WalletWasabi/tree/master/Contrib/CLI).
