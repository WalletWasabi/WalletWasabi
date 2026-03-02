#!/bin/bash

set -e

# Configuration
BITCOIN_DATADIR="/tmp/bitcoin-regtest"
WASABI_DATADIR="/tmp/wasabi"
BITCOIN_RPC_PORT=18443
BITCOIN_P2P_PORT=18444
COORDINATOR_PORT=37126
WALLET_RPC_PORT=37128
TEST_TIMEOUT=600

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m' # No Color

# Print functions
print_header() {
    echo -e "\n${BOLD}${BLUE}═══════════════════════════════════════════════════════════════${NC}"
    echo -e "${BOLD}${BLUE}  $1${NC}"
    echo -e "${BOLD}${BLUE}═══════════════════════════════════════════════════════════════${NC}\n"
}

print_step() {
    echo -e "${CYAN}▶${NC} ${BOLD}$1${NC}"
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_info() {
    echo -e "${YELLOW}ℹ${NC} $1"
}

# Cleanup function
cleanup() {
    print_header "Cleaning up..."

    if [ -n "$COORDINATOR_PID" ]; then
        kill $COORDINATOR_PID 2>/dev/null || true
        print_info "Stopped coordinator (PID: $COORDINATOR_PID)"
    fi

    if [ -n "$WALLET_PID" ]; then
        kill $WALLET_PID 2>/dev/null || true
        print_info "Stopped wallet daemon (PID: $WALLET_PID)"
    fi

    if bitcoin-cli -regtest -rpcport=$BITCOIN_RPC_PORT -rpcuser=regtest -rpcpassword=regtest stop 2>/dev/null; then
        print_info "Stopped Bitcoin node"
        sleep 2
    fi

    rm -rf $WASABI_DATADIR/Client/Wallets 2>/dev/null || true
    rm -rf $BITCOIN_DATADIR 2>/dev/null || true

    print_success "Cleanup complete"
}
trap cleanup EXIT

# Helper function for Bitcoin CLI
btc_cli() {
    bitcoin-cli -regtest -rpcport=$BITCOIN_RPC_PORT -rpcuser=regtest -rpcpassword=regtest "$@"
}

# Helper function with wallet
btc_cli_wallet() {
    bitcoin-cli -regtest -rpcport=$BITCOIN_RPC_PORT -rpcuser=regtest -rpcpassword=regtest -rpcwallet="default" "$@"
}

# Wait for service with timeout
wait_for_service() {
    local description=$1
    local check_command=$2
    local timeout=${3:-30}

    print_step "Waiting for $description..."

    for i in $(seq 1 $timeout); do
        if eval "$check_command" &>/dev/null; then
            print_success "$description is ready"
            return 0
        fi
        printf "${YELLOW}.${NC}"
        sleep 1
    done

    echo
    print_error "Timeout waiting for $description"
    return 1
}

# Start Bitcoin node
print_header "Starting Bitcoin Node"

print_step "Creating data directory"
mkdir -p "$BITCOIN_DATADIR"

print_step "Starting bitcoind in regtest mode"
bitcoind \
  -regtest \
  -datadir="$BITCOIN_DATADIR" \
  -rpcport=$BITCOIN_RPC_PORT \
  -port=$BITCOIN_P2P_PORT \
  -server \
  -rpcuser=regtest \
  -rpcpassword=regtest \
  -blockfilterindex=1 \
  -fallbackfee=0.0001 \
  -daemon

sleep 3

wait_for_service "Bitcoin RPC" "btc_cli getblockchaininfo" || exit 1

print_step "Creating default wallet"
btc_cli createwallet "default" > /dev/null 2>&1 || true

print_step "Loading default wallet"
btc_cli loadwallet "default" > /dev/null 2>&1 || true

print_step "Generating 150 initial blocks"
ADDR=$(btc_cli_wallet getnewaddress)
btc_cli generatetoaddress 150 "$ADDR" > /dev/null
print_success "Initial blockchain setup complete"

# Start Wasabi Coordinator
print_header "Starting Wasabi Coordinator"

WASABI_COORDINATOR_DATADIR="$WASABI_DATADIR/Coordinator"
WASABI_COORDINATOR_LOGFILE="$WASABI_COORDINATOR_DATADIR/Logs.txt"

print_step "Creating coordinator directory"
mkdir -p "$WASABI_COORDINATOR_DATADIR"
rm -f "$WASABI_COORDINATOR_LOGFILE"

print_step "Generating coordinator configuration"
cat > $WASABI_COORDINATOR_DATADIR/Config.json << EOF
{
  "Network": "RegTest",
  "MainNetBitcoinRpcUri": "http://localhost:$BITCOIN_RPC_PORT",
  "TestNetBitcoinRpcUri": "http://localhost:$BITCOIN_RPC_PORT",
  "RegTestBitcoinRpcUri": "http://localhost:$BITCOIN_RPC_PORT/",
  "BitcoinRpcConnectionString": "regtest:regtest",
  "ConfirmationTarget": 108,
  "DoSSeverity": "0.10",
  "DoSMinTimeForFailedToVerify": "31d 0h 0m 0s",
  "DoSMinTimeForCheating": "1d 0h 0m 0s",
  "DoSPenaltyFactorForDisruptingConfirmation": 0.2,
  "DoSPenaltyFactorForDisruptingSignalReadyToSign": 1,
  "DoSPenaltyFactorForDisruptingSigning": 1,
  "DoSPenaltyFactorForDisruptingByDoubleSpending": 3,
  "DoSMinTimeInPrison": "0d 0h 20m 0s",
  "MinRegistrableAmount": "0.00005",
  "MaxRegistrableAmount": "43000.00",
  "AllowNotedInputRegistration": true,
  "StandardInputRegistrationTimeout": "0d 0h 3m 0s",
  "BlameInputRegistrationTimeout": "0d 0h 3m 0s",
  "ConnectionConfirmationTimeout": "0d 0h 1m 0s",
  "OutputRegistrationTimeout": "0d 0h 1m 0s",
  "TransactionSigningTimeout": "0d 0h 1m 0s",
  "FailFastOutputRegistrationTimeout": "0d 0h 3m 0s",
  "FailFastTransactionSigningTimeout": "0d 0h 1m 0s",
  "RoundExpiryTimeout": "0d 0h 5m 0s",
  "MaxInputCountByRound": 10,
  "MinInputCountByRoundMultiplier": 0.5,
  "MinInputCountByBlameRoundMultiplier": 0.4,
  "RoundDestroyerThreshold": 375,
  "CoordinatorExtPubKey": "xpub6C13JhXzjAhVRgeTcRSWqKEPe1vHi3Tmh2K9PN1cZaZFVjjSaj76y5NNyqYjc2bugj64LVDFYu8NZWtJsXNYKFb9J94nehLAPAKqKiXcebC",
  "CoordinatorExtPubKeyCurrentDepth": 1,
  "MaxSuggestedAmountBase": "100",
  "RoundParallelization": 1,
  "CoordinatorIdentifier": "CoinJoinCoordinatorIdentifier",
  "AllowP2wpkhInputs": true,
  "AllowP2trInputs": true,
  "AllowP2wpkhOutputs": true,
  "AllowP2trOutputs": true,
  "AllowP2pkhOutputs": false,
  "AllowP2shOutputs": false,
  "AllowP2wshOutputs": false,
  "DelayTransactionSigning": false,
  "AnnouncerConfig": {
    "CoordinatorName": "Coordinator",
    "IsEnabled": false,
    "CoordinatorDescription": "WabiSabi Coinjoin Coordinator",
    "CoordinatorUri": "https://api.example.com/",
    "AbsoluteMinInputCount": 21,
    "ReadMoreUri": "https://api.example.com/",
    "RelayUris": [
      "wss://relay.primal.net"
    ],
    "Key": "nsec1wax9zrs4r8g57767760j3drg87hgm5mwqtecznxtarrt9zsl6fhqyfdh7l"
  },
  "PublishAsOnionService": false,
  "OnionServicePrivateKey": null
}
EOF

print_step "Launching coordinator process"
dotnet run --project WalletWasabi.Coordinator -- \
  --urls="http://localhost:$COORDINATOR_PORT" \
  --datadir="$WASABI_COORDINATOR_DATADIR" &> /dev/null &
COORDINATOR_PID=$!
print_success "Coordinator started (PID: $COORDINATOR_PID)"

sleep 5

# Start Wasabi Wallet Client
print_header "Starting Wasabi Wallet Daemon"

print_step "Creating client directory"
mkdir -p "$WASABI_DATADIR/Client"

print_step "Launching wallet daemon"
dotnet run --project WalletWasabi.Daemon -- \
  --network=regtest \
  --coordinatorUri="http://127.0.0.1:$COORDINATOR_PORT" \
  --rpcport=$WALLET_RPC_PORT \
  --datadir="$WASABI_DATADIR/Client" \
  --jsonrpcserverenabled=true \
  --maxcoinjoinminingfeerate=500 \
  --absolutemininputcount=4 \
  --usebitcoinrpc=true \
  --bitcoinrpccredentialstring="regtest:regtest" \
  --bitcoinrpcendpoint="http://localhost:$BITCOIN_RPC_PORT" \
  --usetor="disabled" &> /dev/null &

WALLET_PID=$!
print_success "Wallet daemon started (PID: $WALLET_PID)"

sleep 3

wait_for_service "Wallet RPC" \
  "curl -s -X POST http://127.0.0.1:$WALLET_RPC_PORT/ -H 'Content-Type: application/json' -d '{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"listwallets\",\"params\":[]}' | grep -q result" \
  || exit 1

# Wait for filter synchronization to complete
print_step "Waiting for filter synchronization..."
for retry in {1..5}; do
    FILTERS_LEFT=$(curl -s -X POST http://127.0.0.1:$WALLET_RPC_PORT/ \
        -H "Content-Type: application/json" \
        -d '{"jsonrpc":"2.0","id":"1","method":"getstatus","params":[]}' | jq -r '.result.filtersLeft // 999')

    if [ "$FILTERS_LEFT" = "0" ]; then
        break
    fi
    printf "${YELLOW}.${NC}"
    sleep 1
done
echo
print_success "Filters synchronized"

# Create and fund wallets
print_header "Creating and Funding Wallets"

create_and_fund_wallet() {
    local wallet_name=$1
    printf "${CYAN}▶${NC} Creating $wallet_name... "

    # Create wallet (just creates the wallet file, doesn't load it)
    curl -s -X POST http://127.0.0.1:$WALLET_RPC_PORT/ \
        -H "Content-Type: application/json" \
        -d "{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"createwallet\",\"params\":[\"$wallet_name\", \"\"]}" | jq -r '.result' > /dev/null

    printf "${GREEN}✓${NC} Loading... "

    # Load the wallet (this starts the wallet asynchronously)
    curl -s -X POST http://127.0.0.1:$WALLET_RPC_PORT/ \
        -H "Content-Type: application/json" \
        -d "{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"loadwallet\",\"params\":[\"$wallet_name\"]}" > /dev/null

    # Wait for wallet to be fully loaded (poll getwalletinfo until loaded: true)
    local loaded=false
    for retry in {1..120}; do
        local wallet_info=$(curl -s -X POST http://127.0.0.1:$WALLET_RPC_PORT/$wallet_name \
            -H "Content-Type: application/json" \
            -d '{"jsonrpc":"2.0","id":"1","method":"getwalletinfo","params":[]}')

        local is_loaded=$(echo "$wallet_info" | jq -r '.result.loaded // false')

        if [ "$is_loaded" = "true" ]; then
            loaded=true
            break
        fi
        printf "${YELLOW}.${NC}"
        sleep 1
    done

    if [ "$loaded" = "false" ]; then
        echo -e "\n${RED}✗${NC} Wallet $wallet_name failed to load after 120 seconds"
        return 1
    fi

    echo
    printf "${GREEN}✓${NC} Loaded! Funding... "

    # Generate addresses and fund them
    for (( i = 0; i < 4; i++ )); do
        local address=$(curl -s -X POST http://127.0.0.1:$WALLET_RPC_PORT/$wallet_name \
            -H "Content-Type: application/json" \
            -d '{"jsonrpc":"2.0","id":"1","method":"getnewaddress","params":["label"]}' | jq -r '.result.address')

        if [ "$address" = "null" ] || [ -z "$address" ]; then
            echo -e "\n${RED}✗${NC} Failed to get address from $wallet_name"
            return 1
        fi

        btc_cli_wallet sendtoaddress "$address" 1.0 > /dev/null
    done

    echo -e "${GREEN}✓${NC}"
}

start_coinjoin() {
    local wallet_name=$1
    curl -s -X POST http://127.0.0.1:$WALLET_RPC_PORT/$wallet_name \
        -H "Content-Type: application/json" \
        -d '{"jsonrpc":"2.0","id":"1","method":"startcoinjoin","params":[]}' > /dev/null
}

NUM_WALLETS=5

for (( wi = 0; wi < $NUM_WALLETS; wi++ )); do
    create_and_fund_wallet "wallet$wi"
    sleep 1
done

print_success "All $NUM_WALLETS wallets created and funded"

# Generate a block to confirm all transactions
print_header "Confirming Transactions"

print_step "Mining block to confirm funding transactions"
ADDR=$(btc_cli_wallet getnewaddress)
btc_cli generatetoaddress 1 "$ADDR" > /dev/null
print_success "Transactions confirmed"

sleep 2

# Start coinjoins
print_header "Starting CoinJoin Operations"

for (( i = 0; i < $NUM_WALLETS; i++ )); do
    printf "${CYAN}[$((i+1))/$NUM_WALLETS]${NC} Starting coinjoin for wallet$i... "
    start_coinjoin "wallet$i"
    echo -e "${GREEN}✓${NC}"
    sleep 2
done

print_success "All coinjoins initiated"

# Display runtime information
print_header "Runtime Information"
print_info "Coordinator PID: $COORDINATOR_PID"
print_info "Wallet Daemon PID: $WALLET_PID"
print_info "Bitcoin Datadir: $BITCOIN_DATADIR"
print_info "Wasabi Datadir: $WASABI_DATADIR"

# Wait for coinjoin completion
print_header "Monitoring CoinJoin Progress"

print_step "Waiting for coordinator log file"
for i in {1..10}; do
    if [ -f "$WASABI_COORDINATOR_LOGFILE" ]; then
        print_success "Log file found"
        break
    fi
    printf "${YELLOW}.${NC}"
    sleep 1
done
echo

if [ ! -f "$WASABI_COORDINATOR_LOGFILE" ]; then
    print_error "Coordinator log file not found at $WASABI_COORDINATOR_LOGFILE"
    exit 1
fi

print_step "Monitoring for successful coinjoin broadcast (timeout: ${TEST_TIMEOUT}s)"
echo

# Monitor with visual feedback
if timeout $TEST_TIMEOUT grep -q "Successfully broadcast the coinjoin" <(tail -f "$WASABI_COORDINATOR_LOGFILE"); then
    echo
    print_header "SUCCESS!"
    echo -e "${GREEN}${BOLD}  ✓ CoinJoin successfully broadcast!${NC}"
    echo -e "${GREEN}${BOLD}  ✓ Test completed successfully${NC}"
    print_header ""
    exit 0
else
    echo
    print_header "FAILURE"
    print_error "Timeout: No coinjoin after $TEST_TIMEOUT seconds"
    print_info "Check coordinator logs at: $WASABI_COORDINATOR_LOGFILE"
    exit 1
fi
