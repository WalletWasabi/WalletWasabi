# Testing ZeroLink's Mixer

## General

[ZeroLink](https://github.com/nopara73/ZeroLink/) is a wallet privacy framework for round based mixing techniques, like CoinShuffle and TumbleBit's Classic Tumbler mode. It makes sure the user does not get deanonymized by other means, unrelated to the mix, like network analysis. ZeroLink also defines its own mixing technique: Chaumian CoinJoin. It is a massive scale CoinJoin implementation, where the coordinator of the mix is trustless, a round runs within seconds, it cannot steal from the user, nor breach its privacy.  

## Specific

This document guides you through the process on how to help test HiddenWallet's ZeroLink implementation. The results of this test is decisive on the question if we should launch the mixer on the Bitcoin mainnet or not.  

### Time Of Testing

Anonymity likes company. In order to achieve a mix with a reasonable anonymity set we need to coordinate the test to happen in a specific time. However you don't have to be present, you can simply start mixing right now, leave HiddenWallet running and wait for your peers to join the mix.  
A mixing round will kick in if:
- 100 users joined to the mix.  
- **Dec. 20 (Wednesday), 10 PM London time** is reached AND at least 21 users have joined the mix.  

*For convenience: Dec. 20 (Wednesday), 10 PM London is 5:00 PM in New York, 2PM in San Francisco and Dec. 21 (Thursday) 6 AM in Taipei.*

### Success Criteria Of Mainnet Release

If at least 100 users joined the mix and the transaction is on the blockchain, the mixer will launch on the mainnet.
If less than that, the mixer will probably not launch on the mainnet anytime soon.

### Reporting

Please open an issue here, in GitHub.

## Steps

### Step 1: Order a Pizza!

You are not allowed to eat it just yet.

### Step 2: Get HiddenWallet.

If you are using Windows or Linux, you can directly download the [binary release from here](https://github.com/nopara73/HiddenWallet/releases), if you prefer to build it from source code or you are on OSX, then [check out this guide](https://github.com/nopara73/HiddenWallet/blob/master/README.md#building-from-source-code). Don't be afraid of building the software by hand. It is really simple.  

### Step 2.1 Get Tor to your PATH on Linux.

Get Tor on your system: `sudo apt install tor`

### Step 3. Launch the software.  

Unzip the downloaded archive, step into the directory and launch it.  

![](https://i.imgur.com/aYd7xZc.png)

On windows look for `HiddenWallet.exe` and double click on it.  

On Linux:

1. `sudo chmod -R a+rwx ./HiddenWallet-0.6.4-linux`
2. `cd HiddenWallet-0.6.4-linux`
3. `./hiddenwallet`

### Step 4. Generate and decrypt your wallet

When you start the program you will need to generate a wallet. Note, you don't need to give a password, you can just leave it empty for convenience. When you are ready, it will take you to the decryption phase:  

![](https://i.imgur.com/dp0q1nC.png)

Make sure the network is testnet, as the illustration shows, if the wallet tells you: it is establishing the Tor circuit, be patient. The first time it will take from 10 seconds to up to few minutes. You are not allowed to touch the Pizza while waiting.
Finally decrypt your wallet.

### Step 5. Wait until the headers are syncronized.  

Depending on your internet speed, this may take from 3 to 20 minutes. Go grab a coffee. You are still not allowed to touch the Pizza!

### Step 6. Get some testnet coins.

Bitcoin testnet coins are worthless, just google "get bitcoin testnet faucet" and you'll find a site that gives you some. You need at least 0.1 TBTC.  
As you may have noticed, the wallet uses bech32, native segregated witness addresses. You can only register such inputs for mixing, however it creates another problem. Many wallets don't support bech32 just yet. Here's the workaround:  

Under the *[Advanced: Traditional Address]* label you can fund your wallet through a tranditional address, as the following illustration shows:

![](https://i.imgur.com/xAfySIq.png)

You get the money here, then send it to a bech32 address in your wallet:  

![](https://i.imgur.com/UcWQCJh.png)  

Click on the max button, so it will sweep all the funds you have in the wallet to the address you specify.  

You MUST do this step and transfer the funds from the faucet to a bech32 address within HiddenWallet as the mixer will only accept inputs from bech32 addresses.

### Step 7: Eat your Pizza!

While waiting your transactions to be confirmed. You deserve it, you are almost ready.

### Step 8: Mixing.

Finally go the Mixer tab and start mixing:

![](https://i.imgur.com/X4Ut0U7.png)

All you have left to do is to wait for other users to sign up for the mix, or Dec. 20 to come. Thank you for participating!

![](https://i.imgur.com/OFeShT6.png)
