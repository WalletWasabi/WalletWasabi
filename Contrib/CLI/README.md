# Wasabi CLI

A bash script for effortless interaction with the Wasabi RPC Server.

USAGE:

```bash
$ ./wcli.sh [-wallet=<WALLET-NAME>] command [ARGS,...]
```
The supported RPC commands are listed in the [documentation](https://docs.wasabiwallet.io/using-wasabi/RPC.html).

## Examples

```bash
$ ./wcli.sh getstatus

{
  "torStatus": "Running",
  "indexerStatus": "Connected",
  "bestBlockchainHeight": "2432219",
  "bestBlockchainHash": "0000000000000013081887ac34a2bc356a99a3979a808a4e5d63358412cecc68",
  "filtersCount": 5001,
  "filtersLeft": 0,
  "network": "Main",
  "exchangeRate": 29610.18,
  "peers": [
    {
      "isConnected": true,
      "lastSeen": "2023-05-09T18:30:32.4543747+00:00",
      "endpoint": "[::ffff:148.251.1.20]:18343",
      "userAgent": "/Satoshi:0.20.1/"
    },
    {
      "isConnected": true,
      "lastSeen": "2023-05-09T18:29:25.634355+00:00",
      "endpoint": "[::ffff:44.238.21.47]:18333",
      "userAgent": "/btcwire:0.5.0/btcd:0.23.2/"
    }
  ]
}
```

```bash
$ ./wcli.sh -wallet=MyWallet getwalletinfo

{
  "walletName": "MyWallet",
  "walletFile": "/home/ricardo/.walletwasabi/client/Wallets/MyWallet.json",
  "state": "Started",
  "masterKeyFingerprint": "d415c529",
  "anonScoreTarget": 5,
  "isWatchOnly": false,
  "isHardwareWallet": false,
  "isAutoCoinjoin": true,
  "isRedCoinIsolation": false,
  "accounts": [
    {
      "name": "segwit",
      "publicKey": "tpubDDJNwA959utxokPfGjcvV39BAaGoc16YbF1dL3XC7rY388rS1EcG5BjefhzuP4pzMKAhft4X1d6NHRzUL7emJiLwd2xBmeZ9gR3cAcUEB7G",
      "keyPath": "m/84'/0'/0'"
    },
    {
      "name": "taproot",
      "publicKey": "tpubDCVGimU14EWpRjZnbLGDp6uH5St5HWZTcMapVkhb8tWuajRcg99HMbxtSQ9CpSnVHoNGMHMwx3FigonS85iuNmrNEbb2wecB15q1XHTs3br",
      "keyPath": "m/86'/0'/0'"
    }
  ],
  "balance": 198738301,
  "coinjoinStatus": "Idle"
}
```

```bash
$ ./wcli.sh -wallet=MyWallet listkeys | head -10

fullkeypath       internal  keystate  label             scriptpubkey                                                      pubkey                                                              pubkeyhash                                 address
84'/0'/0'/1/0     true      2         x-known-by        cc74ebb140b0ba6314bacd1f908eb2b9eb041717                          039d1e562f46ed0ac30e3abf6f05906f2e42004677f67f2c7c3dd17796553211d3  b41708eb2b9ecc74e0b04a6bb143110bbacd1f97   tb1qkzaxhv2rzjav68us36etnmx8fc9sg9chgzqa3y
84'/0'/0'/0/0     false     2         x-known-by        3ed03e62243c99b3e4cf06678c277b3d6d7b9761                          03c7b418ab3035dab6ce6269d21ba97ccf324a6db0b897c7f786eea1ea685dae62  3497c277b3d63ed034d7b9be6223e6c9cf066781   tb1q8jvmuc3run8sveuvyaan6cldqdxhh9mpgtd6sd
86'/0'/0'/1/0     true      0         x-known-by        8651f36e9e65b81e6bb8fc3fbde66ff6282bc8ded7100a9e8e6a1e84de424d00  03a267175d73a4ca1849fe353e9499d4fc49d915b79f0fbd819eb07dfef56be396  af16812c6914de59af9b5751519b8bc6896b688f   tb1pvkupxm57dwu0c0aauehlv2r9rl5zhjx76ugq4x8x58hgfhjzf5qqlhy7x8
86'/0'/0'/0/0     false     0         x-known-by        47206c52cecbe755ff794f870d3289c5fbd46367162349dd74cb2c9db33bdf02  0371795cd2353da445549c88e195964d143d0d127435ed834620a58963394d73f0  d72ef6d450a704cdf52591f66d0035ec880954a7   tb1pe0n4c5k9lau5lpcdx2yutarjqm4agcm8zc35n46vktwfmvemmupqvjctkz
84'/0'/0'/1/1     true      2         x-known-by        78470de64a6b4703697581b2717e6d2b826d5e83                          02ef9e5370625182643e5782f608902bc4fe00fbac6af8fddc729e62fd6c0702f2  695e17e6d2b878470a26d70de64368b47581b273   tb1qddrsmejrd96crvn30ekjhpuywz3x6h5rav0da9
84'/0'/0'/0/1     false     2         x-known-by        f742250d0a41696f3371eb21b4f62ac91af99d89                          0305f5c2aa51a6c4c972a7202f602081eabdb8f7793937a9abc6a1e1bc894e50c0  439d4f62ac91f7422aaf99650d0f381671eb21b9   tb1qg95k2rg0xdc7kgd57c4vj8m5y240n8vfff39ze
86'/0'/0'/1/1     true      0         x-known-by        8950b84b31724647828bc286c88859f859889d9b5cb59f75b9f55cd88e4bacec  02fdb74cedc8912518e5ff8e2c8d531c75b7bb079474d4b7bc3f2fb1b8db03c1c6  9ce2cebfd391f81e6a94ed272198895cfb0806e9   tb1pwfrysjehs29u9pkg3pvlsky4pvvc38vmtj6e77ul24wd3rjt4nkqux4mnh
86'/0'/0'/0/1     false     0         x-known-by        503a67aaf14652699cff5956cc98c823bdeea7bfdf77b1d5026c2c54512715ca  025655de15dd0eec284fd6a3ca0b11deca53da6fe91e485c92654a40f3d646742f  801560e1101b60296c1cdd31bd11862ab72184a1   tb1pgefx02hennl4j4kvnryz8dgr5cw7afalmammr5pxcfw9g5f8zh9qu96ex4
84'/0'/0'/1/2     true      2         x-known-by        1dcc474cb16e169396a40fed7a0beaa561056843                          02307d454e5ad1ff18fd4d33fb18e5dfe7c03079efba07c357acc257d65402a6a8  6668a0beaa561dcc411056974cb394e1a40fed73   tb1qdctfwn9nj6jqlmt6p0422cwucsgs26zr93kzpz
```

```bash
$ ./wcli.sh -wallet=MyWallet gethistory | head -10

datetime                   height   amount     label        tx                                                                islikelycoinjoin
2018-06-27T17:39:40+00:00  1326503  1000       x-known-by   b2cc6a4f437d915abfd7b851d382102037c3abe7932138fcfb92bc4b5eddd08c  false
2018-07-26T05:25:59+00:00  1355500  50000      x-known-by   6144f487705096397a39d2f0e7e23dd3e9c13d71e9cfc74b04e633eaf2f32a7a  false
2018-07-26T05:25:59+00:00  1355500  -555       x-known-by   ce41d4a5fe0c0da955210bf8722e43af8f580f87a005d7429272bfeb376426ea  false
2018-07-26T05:25:59+00:00  1355500  -720       x-known-by   5429cf93723f37af21f3c4ff5bb11e04af6e79a31aedcd4bbe93e9ae88989e1b  false
2018-07-26T05:25:59+00:00  1355500  -890       x-known-by   405822d6e574019c9f286b0f548d3824a3e305031556fa62a537b13745a0b0a0  false
2018-08-02T17:33:52+00:00  1356647  -17856     x-known-by   a98cca3e7e920bc97b85c17eb256bcaeabf7dec84491a8dfda8850ec4ec9bebf  false
2018-08-02T17:33:52+00:00  1356647  -13764     x-known-by   146eec8341caa0ad7bc63d325e3257e8e88fe4042df25bb9bf25e387909ebe8d  false
2018-08-02T17:52:24+00:00  1356649  219540     x-known-by   07d6708563d3ab4dba975a3058ec6d907bf6c52a1b2061b694548b77bf359dfd  false
2018-08-02T17:52:24+00:00  1356649  -13764     x-known-by   b84e225062daeee4064dd7d13396da0b26bbe644d74b9960f9094aa5424f1a32  false
```

```bash
$ ./wcli.sh -wallet=MyWallet getnewaddress "Ricardo"

{
  "address": "tb1qvxvhvnsfx2vwmnum2erzn6k95m8qc7nh5w4hr5",
  "keyPath": "84'/0'/0'/0/58",
  "label": "Ricardo",
  "publicKey": "0363b35d8f3d1e29f49b4479740dfa6d7cb8a90e0797b60963a32e0139d0f5361b",
  "scriptPubKey": "00146199764e093298edcf9b564629eac5a6ce0c7a77"
}
```
