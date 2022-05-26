# Wasabi Wallet 2.0 Test Vectors

Thank you for considering to test and review Wasabi Wallet 2.0, it's been a long time in the making and we appreciate every feedback so close before release.

Many of the tests can be done on the [testnet](https://en.bitcoin.it/wiki/Testnet), but some require a regtest environment. Follow the setup instructions [here](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/WasabiSetupRegtest.md).

## Installation

- Download the release candidate package, public key and signature
- Verify signature [explained [here](https://docs.wasabiwallet.io/using-wasabi/InstallPackage.html)]
- Install the package

## First Run

- Start Wasabi for the first time on testnet
- Confirm Tor is running, backend is connected
- Confirm filters are downloaded successfully

## Wallet Generation

- Create a new wallet with password
- Create a new wallet without password
- Write wrong password in the first test, see if it fails
- Write correct password, then load wallet [it should show homescreen instantly]

## Wallet Import

- Import an old 1.0 & 2.0 wallet via recovery words, see if balance is correct
- Import an old 1.0 & 2.0 wallet via wallet file, see if balance is correct
- Import a hardware wallet, see if balance is correct

## Receive

- Generate a receive address with a label
- Edit the label
- Hide the address
- Generate a new address and send testnet coins to it, maybe from a faucet:
-- https://testnet-faucet.mempool.co/
-- https://bitcoinfaucet.uo1.net/
-- https://kuttler.eu/en/bitcoin/btc/faucet/
-- https://testnet-faucet.com/btc-testnet/

## Coinjoin / Music box

- Select coinjoin profile for first time, see in wallet file the parameters
- In Wallet Settings, change coinjoin profile, see in wallet files the parameters change
- Manually change the advanced configuration, see in wallet file the parameters change
- Change anonscore target and see privacy level decrease/increase

### Manual start coinjoin

- Turn off auto start coinjoin
- Load wallet, see that music box is in pause
- Click play, see coinjoin start
- Click stop, see coinjoin stop
- Click play, restart Wasabi, load same wallet, see that coinjoin is stopped
- Coinjoin to 100% privacy level, see that coinjoin stops, receive fresh bitcoin to the wallet, see that coinjoin does not start

#### CoinJoin threshold (PlebStop) - retrying

- Set limit (in wallet settings) above your balance
- Click Play
- CoinJoin should be on hold - Pleb stop preventing it
- Set limit below your balance
- Wait - CoinJoin should start automatically at maximum in 1 minute

#### CoinJoin threshold (PlebStop) - stopping

- TODO @molnard

### Auto start coinjoin

- Turn on auto start coinjoin
- Load wallet, see music box is in play
- Click pause, see coinjoin stops
- Click play, see coinjoin starts
- Click pause, restart Wasabi, load same wallet, see that coinjoin is starting
- Coinjoin to 100% privacy level, see that coinjoin stopps, receive fresh bitcoin to the wallet, see that coinjoin is starting

## Send

- Paste all address types [pubkey, script, wrapped segwit, native segwit, taproot, etc]
- Make a typo in address, see it can't continue
- Write amount above wallet balance, see it can't continue
- Write amount of exact wallet balance, see it can continue
- Write amount below wallet balance, see it can continue
- Paste BIP21 URI, see it can't change amount
- Paste BIP21 URI with PayJoin flag, see it can't change amount / address and shows the payjoin logo
- When payment amount below private wallet balance, see no labels on transaction preview screen
- When payment amount above private wallet balance, see labels on transaction preview screen, click on edit, change pocket selection, see results
- Click on privacy optimization, select payment amount higher / lower, see transaction preview amount change
- Click on the undo button, see transaction preview change
- Continue and see password box [only if wallet has password], type in wrong password, see it fails, type in correct password, see it broadcast transaction successfully

## History

- See unconfirmed transactions grouped together
- See multiple coinjoins collapsed into one line item, click expand and see individual coinjoins
- See transaction details icon pop up when hovering, click it to see details dialog
- See right click menu options

## Settings

- Change theme to darkmode
- Turn off Tor, see in status icon that Tor is not connected
- Try Bitcoin full node integration
- Turn on / off auto copy / paste, test if it works in receive and send dialogs
- Turn on start Wasabi on boot

## Close to tray

- Close Wasabi GUI, see it goes into tray icon
- Right click tray icon and maximize GUI
- Right click tray icon and close Wasabi
