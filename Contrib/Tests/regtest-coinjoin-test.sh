#!/bin/bash

set -e

# Configuration
BITCOIN_DATADIR="/tmp/bitcoin-regtest"
WASABI_DATADIR="/tmp/wasabi"
BITCOIN_RPC_PORT=18443
BITCOIN_P2P_PORT=18444
COORDINATOR_PORT=37126
WALLET_RPC_PORT=37128

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

cleanup() {
    kill $COORDINATOR_PID
    kill $WALLET_PID
    bitcoin-cli -regtest -rpcport=$BITCOIN_RPC_PORT -rpcuser=regtest -rpcpassword=regtest stop
    rm -rf $WASABI_DATADIR/Client/Wallets
    rm -rf $BITCOIN_DATADIR
    exit
}
trap cleanup EXIT

echo -e "${YELLOW}Starting Bitcoin node in regtest...${NC}"
mkdir -p "$BITCOIN_DATADIR"

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

echo -e "${GREEN}Bitcoin node started${NC}"

# Wait for bitcoin to be ready
echo -e "${YELLOW}Waiting for Bitcoin RPC to be ready...${NC}"
for i in {1..30}; do
  if bitcoin-cli -regtest -rpcport=$BITCOIN_RPC_PORT -rpcuser=regtest -rpcpassword=regtest getblockchaininfo &>/dev/null; then
    echo -e "${GREEN}Bitcoin RPC is ready${NC}"
    break
  fi
  sleep 1
done

# Create default wallet
echo -e "${YELLOW}Creating default wallet...${NC}"
bitcoin-cli -regtest -rpcport=$BITCOIN_RPC_PORT -rpcuser=regtest -rpcpassword=regtest createwallet "default" > /dev/null 2>&1 || true

# Create default wallet
echo -e "${YELLOW}Loading default wallet...${NC}"
bitcoin-cli -regtest -rpcport=$BITCOIN_RPC_PORT -rpcuser=regtest -rpcpassword=regtest loadwallet "default" > /dev/null 2>&1 || true

# Generate some blocks to have coins
echo -e "${YELLOW}Generating initial blocks...${NC}"
bitcoin-cli -regtest -rpcport=$BITCOIN_RPC_PORT -rpcuser=regtest -rpcpassword=regtest generatetoaddress 150 $(bitcoin-cli -regtest -rpcport=$BITCOIN_RPC_PORT -rpcuser=regtest -rpcpassword=regtest -rpcwallet="default" getnewaddress) > /dev/null

echo -e "${YELLOW}Starting Wasabi Coordinator...${NC}"
mkdir -p "$WASABI_DATADIR/Coordinator"

# Start coordinator in background
WASABI_COORDINATOR_DATADIR="$WASABI_DATADIR/Coordinator"
WASABI_COORDINATOR_LOGFILE="$WASABI_COORDINATOR_DATADIR/Logs.txt"
rm ""$WASABI_COORDINATOR_LOGFILE""

cat > $WASABI_COORDINATOR_DATADIR/Config.json << EOF
{
  "Network": "RegTest",
  "MainNetBitcoinRpcUri": "http://localhost:$BITCOIN_RPC_PORT",
  "TestNetBitcoinRpcUri": "http://localhost:$BITCOIN_RPC_PORT",
  "RegTestBitcoinRpcUri": "http://localhost:$BITCOIN_RPC_PORT",
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

dotnet run --project WalletWasabi.Coordinator -- \
  --datadir="$WASABI_COORDINATOR_DATADIR" &> /dev/null &
COORDINATOR_PID=$!

sleep 5
echo -e "${GREEN}Coordinator started (PID: $COORDINATOR_PID)${NC}"

echo -e "${YELLOW}Starting Wasabi Wallet Client${NC}"

mkdir -p "$WASABI_DATADIR/Client"

# Start wallet daemon
dotnet run --project WalletWasabi.Daemon -- \
  --network=regtest \
  --coordinatorUri="http://127.0.0.1:$COORDINATOR_PORT" \
  --usebitcoinrpc=true \
  --bitcoinrpcendpoint="http://127.0.0.1:$BITCOIN_RPC_PORT/" \
  --bitcoinrpccredentialstring="regtest:regtest" \
  --rpcport=$WALLET_RPC_PORT \
  --datadir="$WASABI_DATADIR/Client" \
  --jsonrpcserverenabled=true \
  --maxcoinjoinminingfeerate=500 \
  --absolutemininputcount=4 \
  --usetor="disabled" &> /dev/null &

WALLET_PID=$!
sleep 3

echo -e "${YELLOW}Creating Wasabi Wallets${NC}"

# Function to start a wallet and perform coinjoin
create_and_fund_wallet() {
  local wallet_name=$1
  local wallet_num=$2

  echo -e "${YELLOW}Create wallet $wallet_name...${NC}"
  curl -s -X POST http://127.0.0.1:$WALLET_RPC_PORT/ \
    -H "Content-Type: application/json" \
    -d "{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"createwallet\",\"params\":[\"$wallet_name\", \"\"]}" | jq -r '.result'

  echo -e "${YELLOW}Generating addresses for $wallet_name...${NC}"
  for (( i = 0; i < 4; i++ )); do
    local address=$(curl -s -X POST http://127.0.0.1:$WALLET_RPC_PORT/$wallet_name \
      -H "Content-Type: application/json" \
      -d '{"jsonrpc":"2.0","id":"1","method":"getnewaddress","params":["label"]}' | jq -r '.result.address')

    echo -e "${YELLOW}Sending funds to $wallet_name ($address)...${NC}"
    bitcoin-cli -regtest -rpcport=$BITCOIN_RPC_PORT -rpcuser=regtest -rpcpassword=regtest -rpcwallet="default"\
      sendtoaddress "$address" 1.0 > /dev/null
  done
}

start_coinjoin()
{
  local wallet_name=$1
  echo -e "${YELLOW}Starting coinjoin...${NC}"
  curl -s -X POST http://127.0.0.1:$WALLET_RPC_PORT/$wallet_name \
      -H "Content-Type: application/json" \
      -d '{"jsonrpc":"2.0","id":"1","method":"startcoinjoin","params":[]}' > /dev/null

  echo -e "${GREEN}Coinjoin initiated for $wallet_name${NC}"
}

# Start multiple wallets and initiate coinjoins
echo -e "${YELLOW}Starting multiple wallets and initiating coinjoins...${NC}"

for (( i = 0; i < 5; i++ )); do
  create_and_fund_wallet "wallet$i" &
  sleep 2
done

echo -e "${GREEN}Wallets created and well funded${NC}"

# Generate a block to confirm
echo -e "${YELLOW}Mine a new block to confirm all transactions${NC}"
bitcoin-cli -regtest -rpcport=$BITCOIN_RPC_PORT -rpcuser=regtest -rpcpassword=regtest \
  generatetoaddress 1 $(bitcoin-cli -regtest -rpcport=$BITCOIN_RPC_PORT -rpcuser=regtest -rpcpassword=regtest -rpcwallet="default" getnewaddress) > /dev/null

sleep 2

echo -e "${YELLOW}Starting coinjoins...${NC}"
for (( i = 0; i < 5; i++ )); do
  start_coinjoin "wallet$i" &
  sleep 2
done

echo -e "${GREEN}All wallets started and coinjoins initiated${NC}"
echo -e "${YELLOW}Bitcoin node PID: $BITCOIN_PID${NC}"
echo -e "${YELLOW}Coordinator PID: $COORDINATOR_PID${NC}"
echo -e "${YELLOW}Bitcoin datadir: $BITCOIN_DATADIR${NC}"
echo -e "${YELLOW}Wasabi datadir: $WASABI_DATADIR${NC}"


# Keep script running
echo -e "${GREEN}Setup complete. Press Ctrl+C to stop all services.${NC}"
TEST_TIMEOUT=600

timeout $TEST_TIMEOUT tail -f "$WASABI_COORDINATOR_LOGFILE" | grep -q "Successfully broadcast the coinjoin"

if [ $? -eq 0 ]; then
  echo -e "${GREEN}WE HAVE A COINJOIN!!!!${NC}"
else
  echo "${RED}Timeout: No coinjoin after $TEST_TIMEOUT seconds"
  exit 1
fi

