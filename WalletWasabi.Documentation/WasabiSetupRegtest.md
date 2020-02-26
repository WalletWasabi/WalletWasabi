# Wasabi Setup on RegTest

## Why do this?

RegTest is a local testing environment in which developers can almost instantly generate blocks on demand for testing events, and can create private satoshis with no real-world value. Running Wasabi Backend on RegTest allows you to emulate network events and observe how the Backend and the Client react on that.
You do not need to download the blockchain for this setup!

## Setup Bitcoin core with RegTest

Follow [this guide](https://bitcoin.org/en/developer-examples#testing-applications).

Todo:
1. Install [Bitcoin core](https://bitcoin.org/en/bitcoin-core/) on your computer.
2. Start Bitcoin core with: bitcoin-qt.exe -regtest then quit immediately - in this way the data directory and the config files will be generated. 
```
Windows: "C:\Program Files\Bitcoin\bitcoin-qt.exe" -regtest
OSX: "/Applications/Bitcoin-Qt.app/Contents/MacOS/Bitcoin-Qt" -regtest
Linux:
```
2. Go to Bitcoin core settings directory. If the directory is missing run core bitcoin-qt, then quit immediately - in this way the data directory and the config files will be generated. 
```
Windows: %APPDATA%\Bitcoin\
OSX: $HOME/Library/Application Support/Bitcoin/
Linux: $HOME/.bitcoin/
```
3. Edit bitcoin.conf file and add these lines there.
```c#
regtest.server = 1
regtest.listen = 1
regtest.whitebind = 127.0.0.1:18444
regtest.rpchost = 127.0.0.1
regtest.rpcport = 18443
regtest.rpcuser = 7c9b6473600fbc9be1120ae79f1622f42c32e5c78d
regtest.rpcpassword = 309bc9961d01f388aed28b630ae834379296a8c8e3
```
4. Start Bitcoin core with: bitcoin-qt.exe -regtest
5. Do not worry about "Syncing Headers" just press the Hide button.
6. Go to MainMenu / Window / Console
7. Generate a new address with:
`getnewaddress`
8. Generate the first 101 blocks with
`generatetoaddress 101 replace_new_address_here`
9. Now you have your own bitcoin blockchain and you are a god there. You can create transactions with the Send button and confirm with:
`generatetoaddress 1 replace_new_address_here`

## Setup Wasabi Backend

Here you will have to build from source, follow [these instructions here](https://github.com/zkSNACKs/WalletWasabi#build-from-source-code).

Todo:
1. Go to WalletWasabi\WalletWasabi.Backend.
2. Open command line and enter:
`dotnet run`
3. You will get some errors but the data directory created. Stop the backend if it is still running with CTRL-C.
4. Go to Wasabi [data directory](https://docs.wasabiwallet.io/using-wasabi/WasabiSetupTails.html#wasabi-data-folder)
```
Windows: "C:\Users\user\AppData\Roaming\WalletWasabi\Backend"
OSX: "/Users/user/.walletwasabi/backend"
Linux:
```
5. Edit Config.json:
```json
{
  "Network": "RegTest",
  "BitcoinRpcConnectionString": "7c9b6473600fbc9be1120ae79f1622f42c32e5c78d:309bc9961d01f388aed28b630ae834379296a8c8e3",
  "MainNetBitcoinP2pEndPoint": "127.0.0.1:8333",
  "TestNetBitcoinP2pEndPoint": "127.0.0.1:18333",
  "RegTestBitcoinP2pEndPoint": "127.0.0.1:18444",
  "MainNetBitcoinCoreRpcEndPoint": "127.0.0.1:8332",
  "TestNetBitcoinCoreRpcEndPoint": "127.0.0.1:18332",
  "RegTestBitcoinCoreRpcEndPoint": "127.0.0.1:18443"
}
```
5. Edit one line in CcjRoundConfig.json. With this the Coordinator waits only 2 participants for CoinJoin. 
```
"AnonymitySet": 2,
```
6. Start Bitcoin core in RegTest 
7. Go to WalletWasabi\WalletWasabi.Backend
8. Open command line and enter:
`dotnet build`
`dotnet run --no-build`
9. Now the Backend is generating the filter and it is running. (You can quit with CTRL-C any time)

## Setup Wasabi Client

Todo:

1. Go to WalletWasabi\WalletWasabi.Gui
`dotnet build`
`dotnet run --no-build`
2. Go to Tools/Settings set the network to RegTest
3. Restart Wasabi with:
`dotnet run --no-build`
4. Generate a wallet in Wasabi named: R1.
5. Generate a receive address in Wasabi, now go to Bitcoin core gui to the Send tab. 
6. Send 1 BTC to Wasabi. 
7. Open another Wasabi instance from another command line: 
`dotnet run --no-build`
8. Generate a wallet in Wasabi named: R2.
9. Generate a receive address in Wasabi, now go to Bitcoin core gui to the Send tab. 
10. Send 1 BTC to Wasabi. 
11. Now in both instance go to CoinJoin tab and enqueue.
12. CoinJoin should happen. 
13. If you see Waiting for confirmation in the Wasabi CoinList you can generate a block in Bitcoin core to continue CoinJoining. 

Happy CoinJoin!

