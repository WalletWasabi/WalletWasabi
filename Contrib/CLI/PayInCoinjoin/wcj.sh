#!/usr/bin/env bash

# Wasabi CoinJoin Payment Runner
# Starts coinjoin and monitors payments, adapting to new/cancelled payments

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

# Handle Ctrl+C gracefully
cleanup() {
    echo ""
    echo "Stopping coinjoin..."
    curl -s $BASIC_AUTH -d '{"jsonrpc":"2.0","id":"1","method":"stopcoinjoin"}' "$RPC_ENDPOINT/$WALLET" > /dev/null
    echo "CoinJoin stopped."
    exit 0
}
trap cleanup SIGINT

get_pending() {
    curl -s $BASIC_AUTH -d '{"jsonrpc":"2.0","id":"1","method":"listpaymentsincoinjoin"}' "$RPC_ENDPOINT/$WALLET" \
        | jq '[.result[] | select(.state[0].status == "Pending")] | sort_by(.address)'
}

show_pending() {
    local payments="$1"
    local count=$(echo "$payments" | jq 'length')
    if [ "$count" -eq 0 ]; then
        echo "  (none)"
    else
        echo "$payments" | jq -r '.[] | "  \(.amount) sats -> \(.address)"'
    fi
}

# Show initial state
echo ""
echo "=== Wallet: $WALLET ==="
echo ""
echo "Pending payments:"
prev=$(get_pending)
show_pending "$prev"
prev_count=$(echo "$prev" | jq 'length')

if [ "$prev_count" -eq 0 ]; then
    echo ""
    read -p "No pending payments. Start coinjoin anyway? [y/N]: " confirm
    if [[ "${confirm^^}" != "Y" ]]; then
        exit 0
    fi
fi

# Start coinjoin
echo ""
curl -s $BASIC_AUTH -d '{"jsonrpc":"2.0","id":"1","method":"startcoinjoin","params":["",false,true]}' "$RPC_ENDPOINT/$WALLET" > /dev/null
echo "=== CoinJoin started ==="
echo ""

# Track payments
prev_addrs=$(echo "$prev" | jq -r '.[].address' | sort)
ever_had_payments=false
if [ "$prev_count" -gt 0 ]; then
    ever_had_payments=true
fi

while true; do
    curr=$(get_pending)
    curr_count=$(echo "$curr" | jq 'length')
    curr_addrs=$(echo "$curr" | jq -r '.[].address' | sort)

    # Track if we ever had payments
    if [ "$curr_count" -gt 0 ]; then
        ever_had_payments=true
    fi

    # Check for completed payments
    if [ -n "$prev_addrs" ]; then
        for addr in $prev_addrs; do
            if ! echo "$curr_addrs" | grep -q "^${addr}$"; then
                amount=$(echo "$prev" | jq -r ".[] | select(.address == \"$addr\") | .amount")
                echo "[$(date +%H:%M:%S)] Sent: $amount sats -> $addr"
            fi
        done
    fi

    # Check for new payments
    if [ -n "$curr_addrs" ]; then
        for addr in $curr_addrs; do
            if [ -z "$prev_addrs" ] || ! echo "$prev_addrs" | grep -q "^${addr}$"; then
                amount=$(echo "$curr" | jq -r ".[] | select(.address == \"$addr\") | .amount")
                echo "[$(date +%H:%M:%S)] Added: $amount sats -> $addr"
            fi
        done
    fi

    # All done? Only exit if we ever had payments and now have none
    if [ "$curr_count" -eq 0 ] && [ "$ever_had_payments" = true ]; then
        break
    fi

    # Update state
    prev="$curr"
    prev_count="$curr_count"
    prev_addrs="$curr_addrs"

    sleep 30
done

# Final state
echo ""
echo "=== All payments done ==="

curl -s $BASIC_AUTH -d '{"jsonrpc":"2.0","id":"1","method":"stopcoinjoin"}' "$RPC_ENDPOINT/$WALLET" > /dev/null
echo "CoinJoin stopped"
