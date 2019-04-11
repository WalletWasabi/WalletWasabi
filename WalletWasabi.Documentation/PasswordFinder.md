# Install:

## Get The Requirements

1. Get Git: https://git-scm.com/downloads
2. Get .NET Core 2.2 SDK: https://www.microsoft.com/net/download
  
## Get and build this software from source code

```sh
git clone https://github.com/lontivero/WasabiPasswordFinder.git
```

## Usage

```
$ dotnet run findpassword -s {encryptedSecret} [OPTIONS]+ 
usage: findpassword --secret:encrypted-secret --language:lang --numbers:[TRUE|
FALSE] --symbold:[TRUE|FALSE]

Tries to find typing mistakes in the user password by brute forcing it char by char.
eg: findpassword --secret:6PYSeErf23ArQL7xXUWPKa3VBin6cuDaieSdABvVyTA51dS4Mxrtg1CpGN --numbers:false --symbold:true

  -s, --secret=VALUE         The secret from your .json file (EncryptedSecret).
  -l, --language=VALUE       The charset to use: en, es, it, fr, pt. Default=en.
  -n, --numbers=VALUE        Try passwords with numbers. Default=true.
  -x, --symbols=VALUE        Try passwords with symbolds. Default=true.
  -h, --help                 Show Help
``` 

You can find your encryptedSecret in your `Wallet.json` file, that you have previously created with Wasabi.


```
dotnet run findpassword -secret:6PYSJ71rbacdSS2htBcpSccutEEEJqGHq3152FuT357ha6iat6BkENGwUB -language:es -numbers -symbols
WARNING: This tool will display you password if it finds it. Also, the process status display your wong password chars.
         You can cancel this by CTRL+C combination anytime.

Enter password: ****
[##############################################################################                      ] 78% - ET: 00:00:47.7226706

Completed in 00:02:51.4221061
SUCCESS: Password found: >>> pato <<<
```

## NOTE

This process is rather slow and CPU heavy. Even for a 10 chars length password it can take significant time to run and
finding the error is not warranted in any case. Please review the code before running it.