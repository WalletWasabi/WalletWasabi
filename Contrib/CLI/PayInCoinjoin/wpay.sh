#!/usr/bin/env bash

# Wasabi Pay in CoinJoin
# Interactive payment queuing with standard denomination suggestions

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

DENOMS=(5000 6561 8192 10000 13122 16384 19683 20000 32768 39366 50000 59049 65536 100000 118098 131072 177147 200000 262144 354294 500000 524288 531441 1000000 1048576 1062882 1594323 2000000 2097152 3188646 4194304 4782969 5000000 8388608 9565938 10000000 14348907 16777216 20000000 28697814 33554432 43046721 50000000 67108864 86093442 100000000 129140163 134217728 200000000 258280326 268435456 387420489 500000000 536870912 774840978 1000000000 1073741824 1162261467 2000000000 2147483648 2324522934 3486784401 4294967296 5000000000 6973568802 8589934592 10000000000 10460353203 17179869184 20000000000 20920706406 31381059609 34359738368 50000000000 62762119218 68719476736 94143178827 100000000000 137438953472)

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
echo "=== Add Payments ==="

add_payment() {
    echo ""
    # Get address
    read -p "Address: " ADDRESS
    if [ -z "$ADDRESS" ]; then
        echo "Address required."
        return 1
    fi

    # Get amount
    read -p "Amount (sats): " AMOUNT
    if ! [[ "$AMOUNT" =~ ^[0-9]+$ ]]; then
        echo "Invalid amount."
        return 1
    fi

    # Find nearest denominations
    local lower=""
    local higher=""
    local exact=""

    for d in "${DENOMS[@]}"; do
        if [ "$d" -eq "$AMOUNT" ]; then
            exact="$d"
            break
        elif [ "$d" -lt "$AMOUNT" ]; then
            lower="$d"
        elif [ "$d" -gt "$AMOUNT" ] && [ -z "$higher" ]; then
            higher="$d"
            break
        fi
    done

    # Calculate differences
    local lower_diff lower_pct higher_diff higher_pct
    if [ -n "$lower" ]; then
        lower_diff=$((AMOUNT - lower))
        lower_pct=$(awk "BEGIN {printf \"%.2f\", ($lower_diff / $AMOUNT) * 100}")
    fi

    if [ -n "$higher" ]; then
        higher_diff=$((higher - AMOUNT))
        higher_pct=$(awk "BEGIN {printf \"%.2f\", ($higher_diff / $AMOUNT) * 100}")
    fi

    # Display options
    echo ""
    echo "Standard denominations blend better in coinjoins."
    echo ""

    local selected
    if [ -n "$exact" ]; then
        echo "Your amount is already a standard denomination."
        selected="$exact"
    else
        echo "Options:"
        echo ""
        if [ -n "$lower" ]; then
            echo "  [L] Send less:  $lower sats  (-$lower_diff sats, -$lower_pct%)"
        fi
        if [ -n "$higher" ]; then
            echo "  [M] Send more:  $higher sats  (+$higher_diff sats, +$higher_pct%)"
        fi
        echo "  [E] Exact amount: $AMOUNT sats  (non-standard)"
        echo ""
        read -p "Choice [L/M/E]: " choice

        case "${choice^^}" in
            L)
                if [ -n "$lower" ]; then
                    selected="$lower"
                else
                    echo "No lower denomination available."
                    return 1
                fi
                ;;
            M)
                if [ -n "$higher" ]; then
                    selected="$higher"
                else
                    echo "No higher denomination available."
                    return 1
                fi
                ;;
            E)
                selected="$AMOUNT"
                ;;
            *)
                echo "Invalid choice."
                return 1
                ;;
        esac
    fi

    # Send payment
    local result error payment_id
    result=$(curl -s $BASIC_AUTH -d '{"jsonrpc":"2.0","id":"1","method":"payincoinjoin","params":["'"$ADDRESS"'",'"$selected"']}' "$RPC_ENDPOINT/$WALLET")
    error=$(echo "$result" | jq -r '.error.message // empty')

    if [ -n "$error" ]; then
        echo "Error: $error"
        return 1
    fi

    payment_id=$(echo "$result" | jq -r '.result')
    echo ""
    echo "Queued: $selected sats -> $ADDRESS"
    echo "Payment ID: $payment_id"
    return 0
}

# Main loop
while true; do
    add_payment
    echo ""
    read -p "Add another payment? [Y/n]: " again
    if [[ "${again^^}" == "N" ]]; then
        break
    fi
done

echo ""
echo "Done. Run wcj.sh to start coinjoin."
