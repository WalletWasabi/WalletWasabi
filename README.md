ATTENTION: The code is half cooked, the wallet is half implemented.

# Hidden Wallet
Easy-to-use, instant, anonymous Bitcoin wallet. (At least that's the goal.)

## For developers
C#, Visual Studio, Clone -> Build -> Run  
Tools/ Settings/ Network -> Test

## Design decisions
Development decisions shall be made by balancing **usability** and **privacy**.  
  
The wallet aims to give the user a feel of an instant, anonymous Bitcoin wallet, but it has its costs, what are expected to be gradually eliminated as the Bitcoin space is evolving.  

## Philosophy

There is a rough consensus among economists about the properties of good money, which are rarity, durability, un-consumability, divisibility, fungibility and portability.  
The Bitcoin core takes care of the first four, wallets has to take care of fungibility and portability.  
Of course there are overlaps, for example centralized services are able to affect the rarity of bitcoins, just like MtGox, what has lead to its collapse.  
An other example would be the pseudonimity of the Bitcoin network or the coming Confidental transactions, what are core level fungibility features. 
But let me not get lost in details and settle at the mental model above.

### How other wallets tackle fungibility and portability?

Most wallets dismiss fungibility, completely relying on the Bitcoin network's pseudonymity, or even worse they implement AML/ KYC regulations, HiddenWallet is none of those.  
Wallets, concentrating on fungibility are doing it for the expense of portability (meaning: they are not convenient to use, e.g. JoinMarket, which is fine, it's a matter of where you try to find the balance).  
Some fungibility provider have other issues: mixers, exchanges, casinos are centralized, blockchain.info doesn't work properly over TOR and DarkWallet is a dead project.  

### How Hidden Wallet tackles fungibility and portability?

The main goal of HiddenWallet is to create the most convenient privacy oriented wallet out there.  

#### Usability

Most of today's Bitcoin wallets suck from an end user perspective.  
This software's target customer is my grandma. Therefore every GUI design decision shall be made by keeping that in mind.  
Exception can only be made if the modification is really cool, extremely funny or causes a programmer boner.

#### Privacy

The software should gravitate towards complete anonymity keeping up and build on the newest technological developments rapidly happening in the space.  
The simplicity of the software can be compromised if privacy is in stake.

#### Windows first, other platforms later

Desktop clients are the most reliable and Windows is the most popular desktop client.  
Although web and mobile platforms are more convenient for end users, on desktops way more stable codebase can be achieved in a shorter timeframe. Mobile and web clients are expected to be built in the future.

#### HD wallet structure
In a HD wallet every private key can be derived from a seed, this simplifies the backup process, compared to a wallet, like Bitcoin QT, that's "just a bunch of keys" and that has to be backuped periodically.  
However this design choice have **privacy** costs. If the wallet gets compromised, the whole transaction history will be visible to the attacker.  
In this case the design decision was **usability** > **privacy**, or put it an other way: don't get hacked! If you do, you are fucked anyway. Probably your funds are more important to you (and to your attacker) than your transaction history. Oh well, let's move on.

#### No address reuse
The wallet forces the user to generate a new address for every incoming transaction by simply not showing already used addresses.  
There are situations when an address has been used multiple times such as for donations, so the wallet has to keep checking the already used addresses, cannot completly throw them away (not like it would be possible with a HD wallet anyway).  
Furthermore every outgoing transaction generates a new address for the change.  
In this case the design decision was **privacy** > **usability**. How easy would everything be if we would only use one address forever, wouldn't it?

#### REST API, SPV, Bitcoin node with pruning, Bitcoin node without pruning
Working with a Bitcoin or SPV node is cumbersome, slow from an end user viewpoint, but for privacy reasons they should be implemented as an option and the user should be educated about their importance.  
REST API is the default for usability reasons, therefore tunneling through TOR and making the web traffic innocent looking with obfsproxy should be implemented. They do not decrease the **usability** of the software (hopefully).

#### Dynamic tx fee calculation
Fees should be hidden from the user (until they are reasonably low) -> **usability**.

#### JoinMarket
I find JoinMarket as the most advanced privacy solution out there and it should be implemented.
