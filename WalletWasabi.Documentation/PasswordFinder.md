# Wasabi Password Finder

Wasabi Password Finder is a tool for helping those who made a mistake typing the password during the wallet creation process. This tool tries to find the password that decrypts the encrypted secret key stored in a given wallet file. 

## Limitations

Wasabi wallet protects the encrypted secret key with the same technology used to protect paper wallets (bip 38) and for that reason it is computationally infeasible to brute force the password using all the possible combinations. It is important to know that Wasabi Password Finder is not for breaking wallet passwords but for finding errors (typos) in an already known password. 

## Usage

To use Wasabi's command line tools on Windows you have to use `wassabeed.exe` that's inside your `Program Files\WasabiWallet`. On Linux and OSX you can use the same software that you use for launching the GUI (`wassabee`).

Let's start giving a glance to the command `help`:

```
$ wassabee run help findpassword
usage: findpassword --wallet:WalletName --language:lang --numbers:[TRUE|FALSE] --symbold:[TRUE|FALSE]

Tries to find typing mistakes in the user password by brute forcing it char by char.
eg: ./wassabee findpassword --wallet:MyWalletName --numbers:false --symbold:true

  -w, --wallet=VALUE         The name of the wallet file.
  -s, --secret=VALUE         You can specify an encrypted secret key instead of wallet. Example of encrypted secret:
                               6PYTMDmkxQrSv8TK4761tuKrV8yFwPyZDqjJafcGEiLBHiqBV6WviFxJV4
  -l, --language=VALUE       The charset to use: en, es, it, fr, pt. Default=en.
  -n, --numbers=VALUE        Try passwords with numbers. Default=true.
  -x, --symbols=VALUE        Try passwords with symbolds. Default=true.
  -h, --help                 Show Help
```

Now, let's find a typo in a wallet called `MagicalCryptoWallet.json`. For the sake of the example let say I've created this wallet and I think the password is `pasd` but it was created with the password `pass` by accident.

```
$ wassabee findpassword --wallet:MagicalCryptoWallet
WARNING: This tool will display you password if it finds it. Also, the process status display your wong password chars.
         You can cancel this by CTRL+C combination anytime.

Enter password: ****    <---- Here I typed the password that I think used to create the wallet (`pasd`)

[##################################################################################                  ] 82% - ET: 00:00:15.4120338

Completed in 00:01:11.5134519
SUCCESS: Password found: >>> pass <<<

```

Note that you can also specify an encrypted secret instead of the wallet file. This is useful if you lost your password for a Bitcoin wallet, other than Wasabi.

Note that for a 4 characters length password it took more than a minute to find. Moreover, the process is heavy in CPU and for that reason it can be a good idea to use the best combination of parameters to reduce the search space.

* __language__ (default: en) specify the alphabet to use and that's important because the number of possible combinations for each character in the password grows rapidily depending on the alphabet used. Just as an example, while the *Italian* charset is "abcdefghimnopqrstuvxyzABCDEFGHILMNOPQRSTUVXYZ", the *French* charset is "aâàbcçdæeéèëœfghiîïjkmnoôpqrstuùüvwxyÿzAÂÀBCÇDÆEÉÈËŒFGHIÎÏJKMNOÔPQRSTUÙÜVWXYŸZ". 

* __numbers__ (default: true) is for indicating that our password contains, or could contain, at least one digit. This increases the charset by 10 (from 0 to 9).

* __symbols__ (default: true) is for indicating that our password contains, or could contain, at least one symbol. This increases the charset by 34 (|!¡@$¿?_-\"#$/%&()´+*=[]{},;:.^`<>). Note that not all symbols are available but only the most common ones instead.


