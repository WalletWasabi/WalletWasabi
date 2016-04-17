# Hidden Wallet
Bringing privacy to my grandma

## For developers
C#, Visual Studio, Clone -> Build -> Run

## Philosophy/ Design principles

There is rough consensus among economists about the properties of good money, which are rarity, durability, un-consumability, divisibility, fungibility and portability.  
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
In order to reach it I have set up the following principles:

#### Simplicity, Clarity, Usability/ Coolness

Most of today's Bitcoin wallets suck from an end user perspective.  
This software's target customer is my grandma. Therefore every GUI design decision shall be made by keeping that in mind.  
Exception can only be made if the modification is cool, extremely funny or causes a programmer boner.

#### Privacy

The software should gravitate towards complete anonymity keeping up and build on the newest technological developments rapidly happening in the space.  
The simplicity of the software can be compromised if privacy is in stake.

#### Windows first, other platforms later

Desktop clients are the most reliable and Windows is the most popular desktop client.  
Although web and mobile platforms are more convenient for end users, on desktops way more stable codebase can be achieved in a shorter timeframe.
