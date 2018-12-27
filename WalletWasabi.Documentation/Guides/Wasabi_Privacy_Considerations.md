# Wasabi Wallet Privacy Considerations

> _Known limitations and how to prevent de-anonymizing yourself while using Wasabi. Note that this list is not comprehensive and other limitations or weaknesses may be found in the future._

### 1. Neutrino is not as private as using one's own full node

The Neutrino protocol used by Wasabi for UTXO confirmation is better for privacy than SPV but still not as private as using one's own full node as the UTXO provider.

### 2. Social media posts could be linked to Wasabi transactions

Users must be careful not to post in a public forum about using Wasabi within minutes or hours or maybe even days of using it to mix their own coins. This could be used to infer that the user was one of the people in one of the ZeroLink rounds that happened in that time frame. Combined with other information, it could lead to the user having their online identity associated with their coins, or any of the coins that participated in the round. (This is potentially problematic if some of the coins from the round during that time frame are later used in a crime that is being investigated.)

### 3. Do not combine ZeroLink outputs

Wasabi users must be careful not to combine coins from each of their ZeroLink outputs `[1]` together i.e. do not use multiple ZeroLink outputs as inputs to the same transaction. Doing so would harm both the user's own anonymity and the anonymity of others who were part of the same ZeroLink round by shrinking the anonymity set.

`[1] Throughout this document, coins sent as an output from a ZeroLink round are referred to as a "ZeroLink output".`

### 4. Treat each ZeroLink output as separate identities

If a Wasabi user uses one ZeroLink output to purchase something from a merchant under a name and uses a different ZeroLink output to purchase something from the same merchant under the same name, the merchant can link those ZeroLink outputs together even though they weren't explicitly linked in the same transaction (ditto for payment processors if the user pays different merchants who happen to use the same centralized payment processor e.g. BitPay and Coinbase Commerce).

### 5. Be careful with same-origin ZeroLink outputs

Similarly to point #4, if a ZeroLink output is associated with the same identity as another ZeroLink output, and the origin of the ZeroLink outputs is the same address, then it would be possible for someone who can associate the identity with the ZeroLink outputs to infer which address they originated from by seeing which origin address `[2]`, of all the addresses in both associated ZeroLink rounds, participated in both rounds.

For example, if Alice uses address bc1abc123 to send 0.1 BTC through ZeroLink to address bc1cba321, then a week later uses address bc1abc123 to send 0.1 BTC through ZeroLink to address bc1dfg456, and Alice uses both ZeroLink outputs to top off her online gaming account, then the gaming company (or anyone with access to their database) would be able to look at the blockchain and determine that the ZeroLink outputs from bc1cba321 and bc1dfg456 both probably originated from bc1abc123 since bc1abc123 is the only address that participated in both rounds that the ZeroLink outputs came from. (If there are other origin addresses that participated in both rounds then this increases the anonymity set, but if Alice's rounds are done far apart then the odds of the rounds sharing multiple common origin addresses are lower. In any case, it's bound to be a relatively small anonymity set.)

`[2] Throughout this document, an address that sends BTC into a ZeroLink round is referred to as an "origin address".`

### 6. Be aware of timing attacks

Users must be careful not to spend outputs from a ZeroLink round (either mixed coins or unmixed change) in close proximity to each other e.g. spending coins from ZeroLink round #1 and then coins from ZeroLink unmixed change within a few minutes of each other. A blockchain observer could infer that the coins from ZeroLink round #1 and the unmixed change are owned by the same person.

### 7. ZeroLink amount limitations could affect the privacy of future transactions

Wasabi's ZeroLink implementation can only perform mixes of ~0.1 BTC at a time. This means that if a user wants to make a transaction smaller than 0.1 BTC that is anonymized, they must first mix 0.1 BTC and spend from the ZeroLink output to send a smaller amount. Any other transactions made with that ZeroLink output can then be linked back to this smaller anonymized transaction.

If a user wants to make a transaction larger than 0.1 BTC that is anonymized, they must first mix enough BTC in 0.1 increments to equal the larger amount they need to spend, then combine all those ZeroLink outputs together to make one larger transaction. This both hurts the anonymity of other ZeroLink users by removing outputs from the anonymity set of the previous ZeroLink round, and hurts the user's own anonymity by making it easier to link their large transaction back to the origin address that send a larger amount of BTC through ZeroLink.

For example, if Alice sends 2 BTC from bc1abc123 through the mixer in 20 rounds, and combines 1.5 BTC from ZeroLink outputs into a single transaction later, then a blockchain observer can infer that the owner of bc1abc123 may also be the same user spending 1.5 BTC worth of ZeroLink outputs from the same rounds that bc1acb123 participated in.

TL;DR It is not easy or advisable after mixing coins through ZeroLink to spend amounts smaller or larger than the exact amount that was mixed in one round because these spend transactions could then be linked together and used to deanonymize the spender (and possiby other users who participated in the same rounds). (See also point #3 and #4 above.)

### 8. Users in the ZeroLink round could be attackers attempting to denanonymize other users

Even with 41 users and 0.1 BTC per round required from each user, Wasabi's ZeroLink rounds are relatively cheap to Sybil attack, at a cost of 4 BTC plus fees to completely deanonymize a single user. There is no guarantee an attacker will be able to fill every other spot in a round, but they can fill _n_ spots per round and decrease the anonymity set by _n_.
