FAQ for [Wasabi Wallet](https://github.com/zkSNACKs/WalletWasabi)
By [6102bitcoin](https://twitter.com/6102bitcoin)

Note: Many of these Q&A have been copied from real users, see footer for acknowledgements. 

# Pre-Install

### What is a “coinjoin”?

A mechanism (proposed by Greg Maxwell) by which multiple participants combine their coins (or UTXOs, to be more precise) into one large transaction with multiple inputs and multiple outputs. An observer cannot determine which output belongs to which input, and neither can the participants themselves. This makes it very difficult for outside parties to trace where a particular coin originated from and where it was sent to (as opposed to regular bitcoin transactions, where there is usually one sender and one receiver).  

In very simple terms, coinjoin means: “when you want to make a transaction, find someone else who also wants to make a transaction and make a joint transaction together”.  

See also: https://en.bitcoin.it/wiki/CoinJoin

### Do I need to trust Wasabi with my coins?

No, Wasabi's coinjoin implementation is trustless by design. The participants don’t need to trust each other or any third party. Both the sending address (before the coinjoin) and the receiving address (after the coinjoin) are controlled by your own private key. Wasabi merely coordinates the process of combining the inputs of the participants into one single transaction, but the wallet can neither steal your coins, nor figure out which outputs belong to which inputs (look up “[Chaumian Coinjoin](https://github.com/nopara73/ZeroLink#ii-chaumian-coinjoin)” if you want to know more).  

### I want to purchase something anonymously. Does coinjoin happen at the point of payment?

No, you should coinjoin at some point before that. After the coinjoin, your coins will be at new addresses which are unlinked from the previous addresses. From there you can make transactions at any time you wish, as with any other regular bitcoin transaction.  
Note that for a coinjoin to happen there needs to be a sufficient number of participants. This might take a few hours during which you need to leave Wasabi open on your computer (so the wallet can sign the transaction when the required number of participants is reached). 

### Will my coins be fully private after mixing with Wasabi?

This depends how you handle your outputs after the coinjoin. There are multiple ways how you can unintentionally undo the mixing by being careless. F.ex, if you make a 1.8 btc transaction into Wasabi, do the coinjoin, and then make one single outgoing transaction of 1.8 btc (minus fees), a third party observer can reasonably assume that both transactions belong to one single entity, due to both amounts being virtually the same even though if they have been through a coinjoin. In this scenario, Wasabi will barely make any improvement to your privacy (it might still have a mild protective effect against unsophisticated observers).  

Another deanonymizing scenario happens when you combine mixed outputs with unmixed ones when sending (a third party will able to make the connection between them as belonging to the same sender). Fortunately, Wasabi’s user interface does make a strong effort to prevent that.  

The practice of being careful with your post-mix outputs is commonly referred to as “coin control”. You can read more about it here: https://medium.com/@nopara73/coin-control-is-must-learn-if-you-care-about-your-privacy-in-bitcoin-33b9a5f224a2. 
See also “Can I recombine my mixed coins?” in the “post-mix” section of this FAQ.

### Can I hurt my privacy using Wasabi?

No. The worst thing that can happen (through user’s negligence post-mix) is that the level of your privacy stays the same as before coinjoin. It is crucial to understand that Wasabi is not a fool-proof solution if you neglect to practice coin control after the mixing process.

### Who is behind Wasabi?

The Company that is developing Wasabi is zkSNACKs LTD ([twitter](https://twitter.com/@Zksnacks_LTD) | [website](https://zksnacks.com/))  

# Install

### How do I install Wasabi?

Follow [this guide](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/Guides/InstallInstructions.md)


### Do I need to run Tor?

All Wasabi network traffic goes via Tor by default - no need to set up Tor yourself. If you do already have Tor, and it is running, then Wasabi will try to use that first.  
You can turn off Tor in the Settings. Note that in this case you are still private, except when you coinjoin and when you broadcast a transaction. In the first case, the coordinator would learn the links between your inputs and outputs based on your IP address, in the second case, if you happen to broadcast a transaction of yours to a full node that is spying on you, it will learn the link between your transaction and your IP address.

# Pre-Mix

### My wallet can't send to Bech32 addresses - what wallets can I use instead? 

Wasabi generates Bech32 addresses only, also known as bc1 addresses or native SegWit addresses. These addresses start with the characters `bc1...` Some wallets/exchanges don't yet support this type of address and may give an error message (e.g. "unknown bitcoin address"). The solution is to manage your funds with a wallet which does support Bech32, [see list](https://en.bitcoin.it/wiki/Bech32_adoption).

Be careful, if you send all your coins from an old wallet to a new wallet (from the table above) in one transaction then you will merge all your coins which is bad for privacy - instead, **send the coins individually** or if possible **import the seed in the new wallet**.

# Mixing

### What are the fees?

You currently pay a fee of 0.003% * anonymity set. If the anonymity set of a coin is 50 then you paid 0.003% * 50 (=0.15%). If you set the target anonymity set to 53 then wasabi will continue mixing until this is reached, so you may end up with an anonymity set of say 60, and you will pay 0.003% * 60 (=0.18%).  
There are also edge cases where you don't pay the full fee or where you pay more. For example if you're the smallest registrant to a round, you'll never pay a fee. Also when you are remixing if you cannot pay the full fee with your input, then you only pay as much as you have, but if the change amount leftover would be too small, then that's also added to the fee. Currently the minimum change amount to be paid out is 0.7% of the base denomination (~0.1BTC.)  
It is also possible that you get more back from mixing than you put in. This happens when network fees go down from when the round started and when the round ended. In this case, the difference is split between the active outputs of the mix.

### What is the Anonymity Set?

The anonymity set is effectively the size of the group you are hiding in. 

If 3 people take part in a CoinJoin (with equal size inputs) and there are 3 outputs then each of those output coins has an anonymity set of 3.

```
0.1 BTC (Alice)       0.1 BTC (Anon set 3)
0.3 BTC (Bob) 	  ->  0.1 BTC (Anon set 3)
0.4 BTC (Charlie)     0.1 BTC (Anon set 3)
                      0.2 BTC (Change Coin Bob)
                      0.3 BTC (Change Coin Charlie)
```

There is no way to know which of the anon set output coins are owned by which of the input owners.
All an observer knows is that a specific anon set output coin is owned by one of the owners of one of the input Coins i.e. 3 people - hence an anonymity set of 3.  
Your Wasabi software has limited information on what the anonymity set should be, so the anonymity set that the software presents you is just an estimation, not an accurate value. With Wasabi we are trying to do lower estimations, rather than upper ones.

### How Do I change the default number of mixing rounds (the Anonymity Set)?

In the Wallet GUI, go to `File`>`Open`>`Config File` and in the last 4 lines you see:

```json
"MixUntilAnonymitySet": 50,
"PrivacyLevelSome": 2,
"PrivacyLevelFine": 21,
"PrivacyLevelStrong": 50
```

You can change the three `PrivacyLevelX` values of the desired anon set of the yellow, green and checkmark shield button in the GUI. The `MixUntilAnonymitySet` is the last selected value from previous use. Remember that you pay a [fee](https://github.com/6102bitcoin/FAQ/blob/master/wasabi.md#what-are-the-fees) proportional to the Anonymity Set. [See more here](https://youtu.be/gWo2RAkIVrE?t=191).

### Can I mix more than the round's minimum? ###

Yes.  
In a round with a ~0.1 BTC minimum, you could mix ~0.3 BTC and get a ~0.1 BTC output & a ~ 0.2 BTC output.
Similarly, with a 0.7 BTC input you would expect the following outputs: ~0.1, ~0.2, ~0.4 BTC. The possible values of equal output that can be created are 0.1 x 2^n where is a positive integer (or zero).  [See more here](https://youtu.be/PKtxzSLPWFU) and [here](https://youtu.be/3Ezru07J674).

### How do I connect my own full node to Wasabi?

There is currently a basic implementation of connecting your full node to Wasabi. The server will still send you [BIP 158 block filters](https://github.com/bitcoin/bips/blob/master/bip-0158.mediawiki), and when you realize that a block contains a transaction of yours, then you pull this block from your own full node, instead of a random P2P node. Thus, you can verify that in this actually is a valid block including your transaction. One attack vector could be that Wasabi lies to you and give you wrong filters that exclude your transaction, thus you would see in the wallet less coins than you actually control. [BIP 157 solves this](https://github.com/bitcoin/bips/blob/master/bip-0157.mediawiki).
When your full node is on the same hardware [computer, laptop] as your Wasabi wallet, then it will automatically recognize it and pull blocks from there. If your node is on a remote device [raspberry pi, nodl, server], then you can specify your local IP in line 11 of the config file. [See more here](https://youtu.be/gWo2RAkIVrE).

### How do I upgrade Wasabi?

You can download the software build for the different operating systems on the main [website](https://wasabiwallet.io) or better over [Tor](http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion). Make sure you also download the signatures of the build and verify them for [Adam Ficsor's public key.](https://github.com/zkSNACKs/WalletWasabi/blob/master/PGP.txt) For step by step instructions, follow [this guide](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/Guides/InstallInstructions.md) or [see this video](https://youtu.be/DUc9A76rwX4).

### Why is the minimum mixing amount a weird number?

The output value changes each round to ensure that you can enqueue a coin and have it remix (mix over and over again - increasing the anonymity set, improving privacy). As a result the round mixing amount will often be a specific number which generally decreases as the rounds proceed, with a reset once a lower bound is reached. 

# Post-Mix

### What do I do now that I have mixed my coins?

Wasabi is a very good wallet, and it is advisable to manage your funds using the wallet once you have mixed. If you are going to send your funds to another wallet (say, a mobile wallet for convenience) then there are a couple of important things to consider.
- Unless the wallet has coin control you will merge your coins which damages privacy. 
- Unless the wallet connects to your own full node you will leak information to the server supplying the blocks/filters.
As a result, there are few wallets that are suitable. 


### Can I recombine my mixed coins?

It is advisable to limit the recombining of mixed coins because it can only decrease the privacy of said coins. This links all the consolidated UTXOs in one transaction, creating only one output, which then clearly controls all these funds. That said, if you combine less than 1 BTC it is less likely to reveal your pre-coinjoin transaction history. The potential issue comes when you spend that coin. Depending on what you do with the coin you might reduce the privacy of the resulting change (if you send half your coin to an exchange for example, as they will know that you own the coin change). As a result it is best not to recombine ALL your mixed change, though you may wish to recombine some coins if you are planning on hodling for many years as this will reduce the fees required to spend the coins later.

If you'd like to dive into the details of this topic, you can [read more here](https://old.reddit.com/r/WasabiWallet/comments/avxbjy/combining_mixed_coins_privacy_megathread/) and [see more here](https://www.youtube.com/watch?v=Tk8-N1kHa4g).

### Am I safe to send my mixed coins to my hardware wallet?

Most hardware wallets communicate with servers to provide you with your balance. This reveals your public key to the server, which damages your privacy - the hardware company can now theoretically link together all your addresses. As a result **it is not recommended** that you send your mixed coins to an address associated with your hardware wallet unless you are confident that you have set up your hardware wallet in a way that it does not communicate with a 3rd party server (see below). 

You can however manage your hardware wallet with the Wasabi interface. Alternatively you can use your hardware wallet with Electrum, which connects to your Bitcoin Core full node through [Electrum Personal Server](https://github.com/chris-belcher/electrum-personal-server).

### How can I set up my hardware wallet with Wasabi properly?

You can use popular hardware wallets **with Wasabi directly** including Coldcard, Trezor and Ledger devices. Plug in the device, and select `Hardware Wallet` in the starting page of Wasabi. You will be able to send and receive bitcoin, but you won't be able to coinjoin the funds directly. For this, the private key needs to be hot in Wasabi. You can also import and export PSBT over SD card to communicate with your ColdCard Wallet.

### Will I have issues spending my mixed coins? 

Not at the moment, if Wasabi and other CoinJoin tools are used by enough people it is likely that this will never be an issue. See this more [comprehensive answer](https://www.reddit.com/r/WasabiWallet/comments/bggy03/will_coinjoined_coins_be_blacklisted_in_the_future/ell04nn?utm_source=share&utm_medium=web2x). 

### What do I do with small changes?

There are no hard and fast rules for what to do with the change. Generally try avoid the change and use the Max button extensively at sending. The most problematic type of change is what has `anonymity set 1` (red shield.) You should treat it as a kind of toxic waste (handled with great care).

**Warning**
You want to avoid merging `anonymity set 1 coins` with `anonymity set > 1 coins` wherever possible, because this will link your `anonymity set > 1 coin` to the coin you merge it with. Note that, this is also true if you merge them in a mix, however that's slightly less problematic, because some blockchain analysis techniques become [computationally infeasible](https://www.comsys.rwth-aachen.de/fileadmin/papers/2017/2017-maurer-trustcom-coinjoin.pdf).

It is also important that you don't send different coins to the same receiving address (even if performed as separate transactions) as this will also link the coins together, damaging your privacy.

**Your Options**
- If you don't care about linking the history of the coins because they are all from the same source then you could combine them in a mix (que all the change from the same source until you reach the minimum input required to mix, currently ~ 0.1 BTC).  
- Mix with [Joinmarket](https://github.com/JoinMarket-Org/joinmarket-clientserver).
- Donate them (e.g. [to the EFF](https://www.eff.org/))
- Spend them on something that isn't a particular privacy risk (eg. gift cards).
- Open a lightning channel. 
- The ultimate solution is to 'close the loop' i.e. spend a change coin without merging it with other coins don't generate it in the first place by sending whole coins. 

# Meta

### Where's the coordinator's source code?

https://github.com/zkSNACKs/WalletWasabi/tree/master/WalletWasabi.Backend

### Does Wasabi have a warrant canary?

The nature of Wasabi is that you shouldn't need to trust the devs or the wasabi coordinating server, as you can verify that the code does not leak information to anyone. The dev's have gone to great lengths in an attempt to ensure that the coordinator can't steal funds nor harvest information (for example, the outputs sent from your wasabi wallet are blinded, meaning that even the wasabi server can't link the outputs to the inputs). 

The only known possible 'malicious' actions that the server *could* perform are two sides of the same coin;
- blacklisted UTXO's
Though this would not affect the users who are able to successfully mix with other 'honest/real' peers. 
- Targeted Sybil Attack 
The follow-up concern is the inverse of the above. It is possible that the server could *only* include one 'honest/real' coin in the mix and supply the other coins themselves. This would give a false sense of security, **but it would not worsen the existing privacy of the coin**. It would also be noticable to all users excluding the user being targeted as their coins would not be mixed. It has been argued that this 'attack' would be very costly in terms of fees because the number of coins being mixed is verifiable. Though it is true that fees would have to be paid to zkSNACKs every round this does not matter if it is zkSNACKs that is acting maliciously (as they get the funds back). Typical rounds currently have <100 people per mix, with the minimum input being ~0.1 BTC with a fee of 0.003% per anonymity set. Taking the 'worst case' (100 people, each mixing 0.1 BTC) gives 0.03 BTC per round. This is not prohibitive and is thus a valid concern. That said, if multiple chain-analysis companies attempt to flood the zkSNACKs mix (to decrease the true anonymity set) they will hinder each other's efforts (unless they are cooperating). See [here](https://github.com/nopara73/ZeroLink/#e-sybil-attack) for more info.

### Where can I learn more about Wasabi?

User created content on Wasabi can be found [in our Dojo](https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Community/Dojo.md).

# Errors

### 'Backend won't connect'

All Wasabi network traffic goes via Tor. When Tor has issues Wasabi has issues. If the Tor Hidden Service directory goes down (which it does occasionally) Wasabi now has a fall-back back to the coordinator server without a hidden service (but still over Tor). 

**It is easiest to wait and try again some hours later.**

Alternatively, you can turn off Tor in the Settings. Note that in this case you are still private, **except when you coinjoin** and **when you broadcast a transaction**. In the first case, the coordinator would learn the links between your inputs and outputs based on your IP address, in the second case, if you happen to broadcast a transaction of yours to a full node that is spying on you, it will learn the link between your transaction and your IP address.

# Acknowledgements

Thanks to the developers of Wasabi - you make it possible for me to spend bitcoin without having to worry about other people knowing how little bitcoin I can afford.

Thanks to the following people for the help that they have provided to wasabi users on the wasabi subreddit which I have condensed into this FAQ.
- [iLoveStableCoins](https://www.reddit.com/user/iLoveStableCoins) a regular poster to the wasabi wallet subreddit. (May now be an employee of [zkSNACKs!](https://old.reddit.com/r/WasabiWallet/comments/b08yme/could_chainalysis_be_participating_with_49_inputs/eifa5fe/))
- Adam Ficsor ([@nopara73](https://twitter.com/nopara73))
- Lucas Ontivero ([@lontivero](https://twitter.com/lontivero/))
- Max Hillebrand ([@hillebrandmax](https://twitter.com/HillebrandMax/)) for fixes made in the [original repo](https://github.com/6102bitcoin/FAQ/blob/master/wasabi.md).

Please issue pull requests if you have suggestions.
