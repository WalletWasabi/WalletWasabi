## wpay.sh - Queue Payments

Queue multiple payments with smart denomination suggestions:

```
$ ./wpay.sh
Wallets:

[1] savings
[2] spending

Select wallet: 2

Loading wallet spending (this may take a moment)...
Wallet ready.

=== Add Payments ===

Address: tb1qxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Amount (sats): 75000

Standard denominations blend better in coinjoins.

Options:

[L] Send less:  65536 sats  (-9464 sats, -12.62%)
[M] Send more:  100000 sats  (+25000 sats, +33.33%)
[E] Exact amount: 75000 sats  (non-standard)

Choice [L/M/E]: m

Queued: 100000 sats -> tb1qxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Payment ID: 65c21ec1-9865-4cd6-bd67-c2f058a45d24

Add another payment? [Y/n]: n

Done. Run wcj.sh to start coinjoin.
```

The script suggests rounding to standard denominations because they blend in with other coinjoin outputs. A payment of exactly 73,847 sats stands out. A payment of 65,536 sats looks like everyone else's change.


## wcancel.sh - Cancel Payments

Made a mistake? Cancel payments interactively:

```
$ ./wcancel.sh
Wallets:

[1] savings
[2] spending

Select wallet: 2

Loading wallet spending (this may take a moment)...
Wallet ready.

=== Pending Payments ===

[1] 100000 sats -> tb1qxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
[2] 50000 sats -> tb1qyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy

[A] Cancel all
[Q] Quit

Cancel which? 2
Cancelled: 50000 sats -> tb1qyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy

Select by number, cancel multiple with 1,3 or 1 3, or cancel all with A.
```

## wcj.sh - Run Coinjoin

Start coinjoin and monitor until all payments complete:

```
$ ./wcj.sh
Wallets:

[1] savings
[2] spending

Select wallet: 2

Loading wallet spending (this may take a moment)...
Wallet ready.

=== Wallet: spending ===

Pending payments:
100000 sats -> tb1qxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
50000 sats -> tb1qyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy

=== CoinJoin started ===

[14:32:15] Sent: 50000 sats -> tb1qyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy
[14:45:02] Sent: 100000 sats -> tb1qxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

=== All payments done ===
CoinJoin stopped
```

The script: - Shows initial state - Starts coinjoin automatically - Detects when payments complete - Detects if you add new payments while running - Stops coinjoin when done - Handles Ctrl+C gracefully

## Setup

#### Enable RPC in Wasabi's Config.json:

> "JsonRpcServerEnabled": true

#### Make scripts executable:

> chmod +x wpay.sh wcancel.sh wcj.sh

#### Requirements: curl, jq

#### Typical Workflow

```
./wpay.sh      # Queue your payments
./wcj.sh       # Start coinjoin, wait for completion
```

That's it. Your payments disappear into the crowd.
