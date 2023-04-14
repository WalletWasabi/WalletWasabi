Wallet diagnostic
----------

Toolset for observe the internal state of wallets.

## Wasabi keys state generation

Create a visual representation from a Wasabi keys dump.

### How to run it

Run Wasabi, open the wallet you are interested in.

Next open a terminal and enter:

```bash
dotnet fsi keygraph.fsx <wallet-name>
```

### The result

![walletkeys.png](img/walletkeys.png)

### Dependencies

None

## Wasabi CoinGraph generation

Create a visual representation from a Wasabi coins dump.

### How to run it

Run Wasabi, open the wallet you are interested in.

Next open a terminal and enter:

```bash
dotnet fsi txgraph.fsx <wallet-name> <initial-txid> | dot -Tpng | feh  -
```

## The result

![txgraph.png](img/txgraph.png)

## Dependencies

- graphviz, required for dot
- feh
