#!/usr/bin/env bash

# Wasabi Cancel Payments in CoinJoin
# Interactive selection to cancel pending payments

function config_extract() {
  jq -r "$1" ~/.walletwasabi/client/Config.json
}

RPC_CREDENTIALS=$(config_extract '.JsonRpcUser + ":" + .JsonRpcPassword')
RPC_ENDPOINT=$(config_extract '.JsonRpcServerPrefixes[0]')
BASIC_AUTH=$([ "$RPC_CREDENTIALS" == ":" ] && echo "" || echo "--user ${RPC_CREDENTIALS}")

# Check RPC connection
status=$(curl -s $BASIC_AUTH --connect-timeout 3 -d '{"jsonrpc":"2.0","id":"1","method":"getstatus"}' "$RPC_ENDPOINT" 2>/dev/null)
if [ -z "$status" ]; then
    echo "Error: Cannot connect to Wasabi RPC at $RPC_ENDPOINT"
    echo "Make sure Wasabi is running and RPC is enabled in Config.json"
    exit 1
fi

# Get wallet list
wallets=$(curl -s $BASIC_AUTH -d '{"jsonrpc":"2.0","id":"1","method":"listwallets"}' "$RPC_ENDPOINT" | jq -r '.result')
wallet_count=$(echo "$wallets" | jq 'length')

if [ "$wallet_count" -eq 0 ]; then
    echo "No wallets found."
    exit 1
fi

# Select wallet
echo "Wallets:"
echo ""
for i in $(seq 0 $((wallet_count - 1))); do
    num=$((i + 1))
    name=$(echo "$wallets" | jq -r ".[$i].walletName")
    echo "  [$num] $name"
done
echo ""
read -p "Select wallet: " wallet_choice

idx=$((wallet_choice - 1))
if [ "$idx" -lt 0 ] || [ "$idx" -ge "$wallet_count" ]; then
    echo "Invalid selection."
    exit 1
fi

WALLET=$(echo "$wallets" | jq -r ".[$idx].walletName")

# Load wallet if not already loaded
echo ""
echo "Loading wallet $WALLET (this may take a moment)..."
load_result=$(curl -s $BASIC_AUTH -d '{"jsonrpc":"2.0","id":"1","method":"loadwallet","params":["'"$WALLET"'"]}' "$RPC_ENDPOINT")
load_error=$(echo "$load_result" | jq -r '.error.message // empty')

if [ -n "$load_error" ] && [[ "$load_error" != *"already"* ]]; then
    echo "Error loading wallet: $load_error"
    exit 1
fi
echo "Wallet ready."
echo ""
echo "=== Pending Payments ==="
echo ""

# Get pending payments
result=$(curl -s $BASIC_AUTH -d '{"jsonrpc":"2.0","id":"1","method":"listpaymentsincoinjoin"}' "$RPC_ENDPOINT/$WALLET")
error=$(echo "$result" | jq -r '.error.message // empty')

if [ -n "$error" ]; then
    echo "Error: $error"
    exit 1
fi

payments=$(echo "$result" | jq -r '.result')
count=$(echo "$payments" | jq 'length')

if [ "$count" -eq 0 ]; then
    echo "No pending payments."
    exit 0
fi

# Display numbered list
for i in $(seq 0 $((count - 1))); do
    num=$((i + 1))
    amount=$(echo "$payments" | jq -r ".[$i].amount")
    address=$(echo "$payments" | jq -r ".[$i].address")
    echo "  [$num] $amount sats -> $address"
done

echo ""
echo "  [A] Cancel all"
echo "  [Q] Quit"
echo ""
read -p "Cancel which? " choice

# Quit
if [[ "${choice^^}" == "Q" ]]; then
    exit 0
fi

# Cancel all
if [[ "${choice^^}" == "A" ]]; then
    echo ""
    ids=$(echo "$payments" | jq -r '.[].id')
    for id in $ids; do
        curl -s $BASIC_AUTH -d '{"jsonrpc":"2.0","id":"1","method":"cancelpaymentincoinjoin","params":["'"$id"'"]}' "$RPC_ENDPOINT/$WALLET" > /dev/null
        amount=$(echo "$payments" | jq -r ".[] | select(.id == \"$id\") | .amount")
        address=$(echo "$payments" | jq -r ".[] | select(.id == \"$id\") | .address")
        echo "Cancelled: $amount sats -> $address"
    done
    exit 0
fi

# Cancel specific numbers (comma or space separated)
selections=$(echo "$choice" | tr ',' ' ')

for sel in $selections; do
    if ! [[ "$sel" =~ ^[0-9]+$ ]]; then
        echo "Invalid selection: $sel"
        continue
    fi

    idx=$((sel - 1))

    if [ "$idx" -lt 0 ] || [ "$idx" -ge "$count" ]; then
        echo "Invalid selection: $sel"
        continue
    fi

    id=$(echo "$payments" | jq -r ".[$idx].id")
    amount=$(echo "$payments" | jq -r ".[$idx].amount")
    address=$(echo "$payments" | jq -r ".[$idx].address")

    cancel_result=$(curl -s $BASIC_AUTH -d '{"jsonrpc":"2.0","id":"1","method":"cancelpaymentincoinjoin","params":["'"$id"'"]}' "$RPC_ENDPOINT/$WALLET")
    cancel_error=$(echo "$cancel_result" | jq -r '.error.message // empty')

    if [ -n "$cancel_error" ]; then
        echo "Failed to cancel [$sel]: $cancel_error"
    else
        echo "Cancelled: $amount sats -> $address"
    fi
done
