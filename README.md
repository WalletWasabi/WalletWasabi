The code is proof of concept. Use it with caution.  
  
# Hidden Wallet
Easy-to-use, instant, anonymous Bitcoin wallet. (None of them are true at this point, but that's the goal.)

## Privacy

The software should gravitate towards complete anonymity keeping up and build on the newest technological developments rapidly happening in the space.  

## Intro

The core of this software is a sample Bitcoin wallet, I wrote: [DotNetWallet](https://github.com/nopara73/DotNetWallet/) - a cross platform Bitcoin wallet implementation in .NET Core for a CodeProject tutorial: [Build a Bitcoin wallet in C#](https://www.codeproject.com/script/Articles/ArticleVersion.aspx?waid=214550&aid=1115639)  
  
The wallet might also will turn out to be the first iteration of a dedicated wallet for a Bitcoin privacy improvement technique, called [TumbleBit](https://github.com/BUSEC/TumbleBit). TumbleBit will be integrated through [NTumbleBit](https://github.com/NTumbleBit/NTumbleBit).  
  
The wallet can communicate with the network through HTTP API and paved the way for a full node integration.  
It has a Command Line Interface.
  
##How to use it
The app is cross-platform, you can try it in any OS. You only need to get [dotnet core](https://www.microsoft.com/net/core).  
After you acquired dotnet core build the software:  

```
git clone https://github.com/nopara73/HiddenWallet    
cd HiddenWallet/src/HiddenWallet/  
dotnet restore  
dotnet build  
```  

Run the app once, it will generate your configuration file, called `Config.json`, then open it:  

```
dotnet run  
gedit Config.json  
```

Default config looks like this:

```
{
  "DefaultWalletFileName": "Wallet.json",  
  "Network": "Main",  
  "ConnectionType": "Http",  
  "CanSpendUnconfirmed": "False"  
}
```

**"Network"** can be `"Main"` or `"TestNet"`. Change to testnet for the shake of practice.  
**"ConnectionType"** can be `"Http"` or `"FullNode"`. Full node mode is not implemented.  
**"CanSpendUnconfirmed"** can be `"False"` or `"True"`. Change to "True", so you can play around with it without waiting for confirmations.  

###Walkthrough  
  
Generate a wallet with `dotnet run generate-wallet`.  
**Output:**
```
Choose a password:
***
Confirm password:
***

Wallet is successfully created.
Wallet file: Wallets/Wallet.json

Write down the following mnemonic words.
With the mnemonic words AND your password you can recover this wallet by using the recover-wallet command.

-------
virus into smooth shock eternal task guitar bus glide taste glow barrel
-------
```
Recover your wallet to an other wallet file:  `dotnet run recover-wallet wallet-file=recovered.json`  
**Output:**
```
Your software is configured using the Bitcoin TestNet network.
Provide your mnemonic words, separated by spaces:
virus into smooth shock eternal task guitar bus glide taste glow barrel
Provide your password. Please note the wallet cannot check if your password is correct or not. If you provide a wrong password a wallet will be recovered with your provided mnemonic AND password pair:
***

Wallet is successfully recovered.
Wallet file: Wallets/recovered.json
```
[Get some testnet bitcoins](http://tpfaucet.appspot.com/) to the wallet. You can get unused addresses with:  `dotnet run receive`  
**Output:**
```
Type your password:
***
Wallets/Wallet.json wallet is decrypted.
7 Normal keys are processed.

---------------------------------------------------------------------------
Unused Receive Addresses
---------------------------------------------------------------------------
mzz63n3n89KVeHQXRqJEVsQX8MZj5zeqCw
mhm1pFe2hH7yqkdQhwbBQ8qLnMZqfL6jXb
mmRzqMDBrfNxMfryQSYec3rfPHXURNapBA
my2ELDBqLGVz1ER7CMynDqG4BUpV2pwfR5
mmwccp4GefhPn4P6Mui6DGLGzHTVyQ12tD
miTedyDXJAz6GYMRasiJk9M3ibnGnb99M1
mrsb39MmPceSPfKAURTH23hYgLRH1M1Uhg
```
Check out your balances:  `dotnet run show-balances`  
**Output:**
```
Type your password:
***
Wallets/Wallet.json wallet is decrypted.
7 Normal keys are processed.
14 Normal keys are processed.
7 Change keys are processed.

---------------------------------------------------------------------------
Address					Confirmed	Unconfirmed
---------------------------------------------------------------------------
mhm1pFe2hH7yqkdQhwbBQ8qLnMZqfL6jXb	0.00000000	0.00880000
mmwccp4GefhPn4P6Mui6DGLGzHTVyQ12tD	0.00000000	0.02710000

---------------------------------------------------------------------------
Confirmed Wallet Balance: 0.00000000
Unconfirmed Wallet Balance: 0.03590000
---------------------------------------------------------------------------
```
Check out your wallet history: `dotnet run show-history`
```
Type your password:
***
Wallets/Wallet.json wallet is decrypted.
7 Normal keys are processed.
14 Normal keys are processed.
7 Change keys are processed.

---------------------------------------------------------------------------
Date			Amount		Confirmed	Transaction Id
---------------------------------------------------------------------------
12/1/16 11:18:33 PM	0.00880000	False		fe9fe4b8ea249097eccb0e4efa7395e4a8ef3c5f084c6264ccb73d1dc634e954
12/1/16 11:19:17 PM	0.00870000	False		704599df454ac0d3e4c1d5ad9f0debd848493dc1f7695e9b455eca934106e4a9
12/1/16 11:19:23 PM	0.00870000	False		c5881cc2ce2386fb320ad60d46762027a2c977532b2773795e613238e29c8bcc
12/1/16 11:19:43 PM	0.00870000	False		986fe7630d66978cfa888df6b47303e97051a0cbf4145a9787b493fbe18b2d42
12/1/16 11:20:41 PM	0.00100000	False		ffb04388d228c52135d4e2212245c91c116b6f3a228418831715065ffcb056d9
```
Send some money to a random address: `dotnet run send address=mxYpuqcSRbdFBgUkeErYSCT14Em72ZUTQn btc=0.016`
```
Type your password:
***
Wallets/Wallet.json wallet is decrypted.
7 Normal keys are processed.
14 Normal keys are processed.
7 Change keys are processed.
Finding not empty private keys...
Select change address...
1 Change keys are processed.
Gathering unspent coins...
Calculating transaction fee...
Fee: 0.000275btc

The transaction fee is 2% of your transaction amount.
Sending:	 0.016btc
Fee:		 0.000275btc
Are you sure you want to proceed? (y/n)
y
Selecting coins...
Signing transaction...
Transaction Id: 3069a436113022d1977b9a9c1a9f233d3aa77b8cd9b6876c8c7dfa0f909ada5e
Broadcasting transaction...

Transaction is successfully propagated on the network.
```
Finally send all your money from the address to somewhere: `dotnet run send address=miasyhU2EhANWVGAoD9PicPACCUhUzdDN4 btc=0.01`

```
Type your password:

Wallets/test wallet is decrypted.
7 Receive keys are processed.
14 Receive keys are processed.
7 Change keys are processed.
14 Change keys are processed.
Finding not empty private keys...
Select change address...
1 Change keys are processed.
2 Change keys are processed.
3 Change keys are processed.
4 Change keys are processed.
5 Change keys are processed.
6 Change keys are processed.
Gathering unspent coins...
Calculating transaction fee...
Fee: 0.00025btc

The transaction fee is 2% of your transaction amount.
Sending:	 0.01btc
Fee:		 0.00025btc
Are you sure you want to proceed? (y/n)
y
Selecting coins...
Signing transaction...
Transaction Id: ad29443fee2e22460586ed0855799e32d6a3804d2df059c102877cc8cf1df2ad
Try broadcasting transaction... (1)

Transaction is successfully propagated on the network.
```  
  
You can specify an optional `wallet-file=` argument to any command if you wish not to use the default wallet file, like `wallet-file=testwallet.json`.  
