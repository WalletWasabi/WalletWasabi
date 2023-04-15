Message Signer Key Generator
----------

Wasabi coordinator must sign the messages it sends to the affiliation servers to allow partners to verify the authenticity of the query and to
be able to use the signed coinjoin notification as a proof against the Wasabi coordinator's operator.

`signerkeygen.fsx` is a script to generate the secp256r1 secret/public key pair. How to use it:

```asxx
$ dotnet fsi signerkeygen.fsx
secretKey: 307702010104200d840751e69baea7eff67bcbb0127456212dd8527c4f13cd6c060c9b69bfd7caa00a06082a8648ce3d030107a1440342000434117d20172fba255253d8b1474144cd8617786c249890517d0e377139af681fe08fad98071334796d97eba280ffaa5e04d65fc68f1860dc39a391a23a048c8f
publicKey: 3059301306072a8648ce3d020106082a8648ce3d0301070342000434117d20172fba255253d8b1474144cd8617786c249890517d0e377139af681fe08fad98071334796d97eba280ffaa5e04d65fc68f1860dc39a391a23a048c8f
```

The `secretKey` must be settled as value in the coordinators `WabiSabiConfig.json` file in the `AffiliationMessageSignerKey` field while the `publicKey` must be
shared with all the partners acting as affiliates (those running an affiliation server).


Revenue Sharing Calculator
--------------

A script to calculate the total amount of bitcoins from affiliate.

This script takes the coinjoin notifications provided by the affiliated which proves the affiliation of unmixed inputs and calculates the total amount of
bitcoins coming from the affiliated. Given Wasabi doesn't store any kind of information related to affiliations, it is the affiliated the one that needs
to prove the affiliation of unmixed inputs by presenting the coinjoin notifications.

### Example

This command runs the revenue.fsx script which parses the coinjoin notifications located in the directory `notification`. It verifies the notification is signed
by the coordinator, that's why we must specify the coordinator's affiliation `pubkey`. The `connection` string is for authenticating agains the bitcoin node's
RPC interface (`user:password`, `cookiefile`) which is required to verify the coinjoin transaction referred in the coinjoin notification really happened and
it is in the blockchain. Finally, `affiliate` is also required because a coinjoin notification is valid only for one specific affiliated.

```bash
$ dotnet fsi revenue.fsx -- --network=TestNet \
     --connection=<rpc-connection@> \
      --path=notification \
      --pubkey=3059301306072a8648ce3d020106082a8648ce3d03010703420004f267804052bd863a1644233b8bfb5b8652ab99bcbfa0fb9c36113a571eb5c0cb7c733dbcf1777c2745c782f96e218bb71d67d15da1a77d37fa3cb96f423e53ba \
      --affiliate=WalletWasabi
```

Optional argument `coordinationFeeRate` can also be specified and its default value is `0.003`

The result can be seen below:

```
coinjoin: 25aaef88eec92b18368c52fc3ef5f602f8dac1caea3cd9d5ee2d80ac81e61784 - Total amount: 6669693 satoshis. Share: 20009
          4319673 0C155D64B106A6E09853F61726BBEA113A434D5528516CE1058A6FC045666294:0
          2350020 38DED71FC19FB8189B8A90CB545CDDCAA9345FBE7ACC0E605347D9ED260FF00C:0
coinjoin: e43ed0cadf6997e96c2412a8cf0a0773fb735a52482f3ddcb7669ba8f7ba7bfc - Total amount: 0 satoshis. Share: 0
coinjoin: fbae1225f9443d88b22551e44fc90c2f9a0c88b33e868de3dd8b481a88e4171f - Total amount: 1062882 satoshis. Share: 3188
          1062882 A6AA999F9C07A73BB877D46213801FD77A9324ACD06F137915B7467EB175B18F:1
Total revenue to share: 0.00023197 btc.
```

There were three coinjoins. The first one contained two affiliated coins, the second one contained no affiliated coins and the third one contained one
affiliated coin.

This affiliated contributed a total of 0.00023197 btc in coordination fees.

### Useful tip

In case you don't have an indexed bitcoin node locally you can map the RPC port of one remote node as follow:

```bash
$ ssh -N -L remote-rpc-port:localhost:local-rpc-port server
```

For example, for testnet this would look like:

```bash
$ ssh -N -L 18332:localhost:18332 zk-testing
```
