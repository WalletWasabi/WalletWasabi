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

