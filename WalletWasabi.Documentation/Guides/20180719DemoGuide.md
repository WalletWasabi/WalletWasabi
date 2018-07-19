- At 2018.07.19 17:00, UTC I will do a live demo of Wasabi on Youtube.
- I will post the livestream link to Twitter: https://twitter.com/nopara73
- You are free to ask questions in the chat.
- At the end of the stream, we will do a mixing round on the mainnet. This will be the first ever Chaumian CoinJoin mainnet mix, you are free to participate.
- In order to participate, you may want to make sure to setup and pre-found the wallet until the stream by going through the following instructions.

## Notes
- Wasabi is in alpha stage. Beta version will be released in 11 days (August 1.)
- The minimum denomination of the mix is 0.1 bitcoins, you may want prefund your wallet with a bit more than that to make sure to cover the fees. Currently the minimum is 0.10034 BTC, but to be sure you may want to prefund it with 0.11 BTC or something like that.
- The minimum participant number of the mix is 100 users. I will cheat and lower this number for the sake of this test.
- The wallet can only generate bech32 addresses. Thus if you are using a legacy wallet, it won't be able to send funds to that. If that is the case you may want to introduce a middle-wallet like [Electrum,](https://electrum.org/).
- Please do not consider trying this out on OSX, as our user interface is unstable on OSX.

## Get The Requirements

1. Get Git: https://git-scm.com/downloads
2. Get .NET Core: https://www.microsoft.com/net/download/dotnet-core/
3. [OSX] Get Brew: https://stackoverflow.com/a/20381183/2061103
4. Get Tor:  
  [Windows] Install the Tor Expert Bundle: https://www.torproject.org/download/  
  [Linux] `apt-get install tor`  
  [OSX] `brew install tor`  
  
## Get Wasabi

Clone & Restore & Build

```sh
git clone https://github.com/zkSNACKs/WalletWasabi
cd WalletWasabi
git submodule update --init --recursive
cd WalletWasabi.Gui
dotnet restore && dotnet build
```

## Run Wasabi

1. Run Tor:  
  [Windows] Run `tor.exe`.  
  [Linux&OSX] Type `tor` in terminal.  
2. Run Wasabi with `dotnet run` from the `WalletWasabi.Gui` folder.


## In-Wallet

1. Generate a new wallet.
2. Backup the mnemonic words.
3. Load the wallet you've just generated.
4. Generate a new Receive Address and fund it with 0.11 BTC.
5. Wait until the filters are synchronized (5 minutes), so you'll see the incoming transaction.
6. Wait until your transaction confirmed.
7. You can already start mixing in the CoinJoin tab if you want.

## Note, You Can Update Wasabi With These Commands:

```sh
git pull
git submodule update --init --recursive 
```
