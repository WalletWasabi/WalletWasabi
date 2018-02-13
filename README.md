# Magical Crypto Wallet

| Windows | Linux & OSX
| :---- | :------ |
[![Windows build status][1]][2] | [![Linux & OSX build status][3]][4] |

[1]: https://ci.appveyor.com/api/projects/status/5om770ij8gnykqab?svg=true
[2]: https://ci.appveyor.com/project/nopara73/magicalcryptowallet
[3]: https://travis-ci.org/nopara73/MagicalCryptoWallet.svg?branch=master
[4]: https://travis-ci.org/nopara73/MagicalCryptoWallet

## Roadmap

**Goal:** Stability, feature minimalization.

It is expected for the proposed timeline to take roughly 1.5 times longer.

| Status                  | Name                 | Time Frame | Dependencies                                    | Expertise                           | Description                                                                                                                                                    |
|-------------------------|----------------------|------------|-------------------------------------------------|-------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|
| <ul><li> [ ] </li></ul> | Wallet Back End      | 2 weeks    |                                                 | NBitcoin, Bitcoin Core RPC, ASP.NET | Build back end for the wallet based on [the specification](https://github.com/nopara73/MagicalCryptoWallet/blob/master/MagicalCryptoWallet.Backend/README.md). |
| <ul><li> [ ] </li></ul> | Small Tor Library    | 1 week     |                                                 | Tor, .NET Core, cross platform      | Build a small Tor library based on DotNetTor, that removes features those are unrelated to the wallet and makes the rest more stable.                          |
| <ul><li> [ ] </li></ul> | Key Manager          | 1 week     |                                                 | NBitcoin                            | Build a new, high performance key manager with accounts, labelling, etc...                                                                                     |
| <ul><li> [ ] </li></ul> | Wallet Client        | 1 month    | Wallet Back End, Small Tor Library, Key Manager | NBitcoin                            | Build a high performance client that can work with the new back end, which is built for client side filtering.                                                 |
| <ul><li> [ ] </li></ul> | ZeroLink v2 Revision | 1 month    |                                                 | Bitcoin privacy, cryptography       | Revise ZeroLink, based on technological advancements.                                                                                                          |
| <ul><li> [ ] </li></ul> | ZeroLink Coordinator | 2 weeks    | ZeroLink v2 Revision                            | NBitcoin, Bitcoin Core RPC          | Revise the ZeroLink Coordinator code based on ZeroLink v2 Revision.                                                                                            |
| <ul><li> [ ] </li></ul> | ZeroLink Client      | 2 weeks    | ZeroLink v2 Revision, ZeroLink Coordinator      | NBitcoin                            | Revise the ZeroLink Client code based on ZeroLink v2 Revision.                                                                                                 |
| <ul><li> [ ] </li></ul> | GUI                  | 1 month    | ALL previous items                              | Electron, front end                 | Redesign the user experience and build it.                                                                                                                     |
| <ul><li> [ ] </li></ul> | Documentation        | 1 week     | ALL previous items                              | Bitcoin                             | Create documentation.                                                                                                                                          |
| <ul><li> [ ] </li></ul> | Internal Testing     | 2 weeks    | ALL previous items, except Documentation        | Bitcoin or .NET or front end        | Test the software and fix the bugs (if there is any haha).                                                                                                     |
| <ul><li> [ ] </li></ul> | Deploy To Mainnet    | 2 weeks    | ALL previous items, except Documentation        | .NET Core, ASP.NET Core deployment  | Deploy the software to Bitcoin Mainnet.                                                                                                                        |
| <ul><li> [ ] </li></ul> | Mainnet Beta Testing | 2 weeks    | ALL previous items                              | More User The Better                | It's rather a marketing phase. The goal is to get at least 1 round done with > 100 user.                                                                       |
| <ul><li> [ ] </li></ul> | Maintanence          | ∞          | ALL previous items                              | NBitcoin                            |                                                                                                                                                                |
