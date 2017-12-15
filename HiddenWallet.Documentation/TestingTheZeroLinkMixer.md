# Testing ZeroLink's Mixer

## General

ZeroLink is a wallet privacy framework for round based mixing techniques, like CoinShuffle and TumbleBit's Classic Tumbler mode. It makes sure the user does not get deanonymized by other means, unrelated to the mix, like network analysis. ZeroLink also defines its own mixing techniuqe: Chaumian CoinJoin. It is a massive scale CoinJoin implementation, where the coordinator of the mix is trustless, a round runs within seconds, it cannot steal from the user, nor breach its privacy.  

## Specific

This document guides you through the process on how to help test HiddenWallet's ZeroLink implementation. The results of this test is decisive on the question if we should launch the mixer on the Bitcoin mainnet or not.  

### Time Of Testing

Anonymity likes company. In order to achieve a mix with reasonable anonymity set we need to coordinate the test to happen in a specific time. However you don't have to be present, you can simply start mixing right now and let it run, wait for your peers to join to the mix.  
A mixing round will kick in if:
- 100 users joined to the mix.  
- **Dec. 20 (Wednesday), 10 PM London time** AND at least 21 users joined to the mix.  

*For convenience: Dec. 20 (Wednesday), 10 PM London is 5:00 PM in New York, 2PM in San Francisco and Dec. 21 (Thursday) 6 AM in Taipei.*

### Success Criteria Of Mainnet Release

If at least 100 users joined the mix and the transaction is on the blockchain, the mixer will launch on the mainnet.
If less than that, the mixer will probably not launch on the mainnet anytime soon.

### Reporting

Please open an issue here, in GitHub.

## Steps

### Step 1: Order a pizza!

You are not allowed to eat it just yet.
