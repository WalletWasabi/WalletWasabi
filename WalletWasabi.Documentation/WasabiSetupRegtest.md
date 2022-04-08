# Wasabi Setup on RegTest

## Why do this?

RegTest is a local testing environment in which developers can almost instantly generate blocks on demand for testing events, and create private satoshis with no real-world value. Running Wasabi Backend on RegTest allows you to emulate network events and observe how the Backend and the Client react on that.
You do not need to download the blockchain for this setup!

## Setup Bitcoin Knots with RegTest

Bitcoin Knots is working very similarly to Bitcoin Core. You can get a grasp with [this guide](https://bitcoin.org/en/developer-examples).

Todo:

1. Install [Bitcoin Knots](http://bitcoinknots.org/) on your computer. Verify the PGP - there is a tutorial [here](http://bitcoinknots.org/)
2. Start Bitcoin Knots with: bitcoin-qt.exe -regtest then quit immediately. In this way the data directory and the config files will be generated.
```
Windows: "C:\Program Files\Bitcoin\bitcoin-qt.exe" -regtest
macOS: "/Applications/Bitcoin-Qt.app/Contents/MacOS/Bitcoin-Qt" -regtest
Linux:
```
3. Go to Bitcoin Core data directory. If the directory is missing run core bitcoin-qt, then quit immediately. In this way the data directory and the config files will be generated.
```
Windows: %APPDATA%\Bitcoin\
macOS: $HOME/Library/Application Support/Bitcoin/
Linux: $HOME/.bitcoin/
```
4. Edit bitcoin.conf file and add these lines there:
```C#
regtest.server = 1
regtest.listen = 1
regtest.txindex = 1
regtest.whitebind = 127.0.0.1:18444
regtest.rpchost = 127.0.0.1
regtest.rpcport = 18443
regtest.rpcuser = 7c9b6473600fbc9be1120ae79f1622f42c32e5c78d
regtest.rpcpassword = 309bc9961d01f388aed28b630ae834379296a8c8e3
```
5. Start Bitcoin Core with: bitcoin-qt.exe -regtest.
6. Do not worry about "Syncing Headers" just press the Hide button. Because you run on Regtest, no Mainnet blocks will be downloaded.
7. Go to MainMenu / Window / Console.
8. Generate a new address with:
`getnewaddress`
9. Generate the first 101 blocks with:
`generatetoaddress 101 <replace_new_address_here>`
10. Now you have your own Bitcoin blockchain and you are a God there - try to resist the insurmountable temptation to start your own shit coin, remember there is only one true coin. You can create transactions with the Send button and confirm with:
`generatetoaddress 1 <replace_new_address_here>`

You can force rebuilding the txindex with the `-reindex` command line argument.

## Setup Wasabi Backend

Here you will have to build from source, follow [these instructions here](https://github.com/zkSNACKs/WalletWasabi#build-from-source-code).

Todo:
1. Go to `WalletWasabi\WalletWasabi.Backend` folder.
2. Open the command line and enter:
`dotnet run`
3. You will get some errors, but the data directory will be created. Stop the backend if it is still running with CTRL-C.
4. Go to the Backend folder:
```
Windows: "C:\Users\{your username}\AppData\Roaming\WalletWasabi\Backend"
macOS: "/Users/{your username}/.walletwasabi/backend"
Linux: "/home/{your username}/.walletwasabi/backend"
```
5. Edit `Config.json` file by replacing everything with:
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
6. Edit one line in `CcjRoundConfig.json` file. With this the Coordinator waits only 2 participants for CoinJoin.
```
"AnonymitySet": 2,
```
7. Start Bitcoin Core in RegTest.
8. Go to WalletWasabi folder
9. Open the command line and enter. This will build all the projects under this directory. 
`dotnet build`
10. Go to WalletWasabi\WalletWasabi.Backend folder.
`dotnet run --no-build`
11. Now the Backend is generating the filters and it is running. (You can quit with CTRL-C any time)

## Setup Wasabi Client

Todo:

1. Go to `WalletWasabi\WalletWasabi.Fluent.Desktop` folder.
2. Open the command line and run the Wasabi Client with:
`dotnet run --no-build`
3. Go to Tools/Settings and set the network to RegTest
4. Close Wasabi and restart it with:
`dotnet run --no-build`
5. Generate a wallet in Wasabi named: R1.
6. Generate a receive address in Wasabi, now go to Bitcoin Core gui to the Send tab.
7. Send 1 BTC to that address.
8. Open another Wasabi instance from another command line:
`dotnet run --no-build`
9. Generate a wallet in Wasabi named: R2.
10. Generate a receive address in Wasabi, now go to Bitcoin Core gui to the Send tab.
11. Send 1 BTC to that address.
12. Now in both instance go to CoinJoin tab and enqueue. CoinJoin should happen.
13. If you see Waiting for confirmation in the Wasabi CoinList you can generate a block in Bitcoin Core to continue coinjoining.

Happy CoinJoin!
