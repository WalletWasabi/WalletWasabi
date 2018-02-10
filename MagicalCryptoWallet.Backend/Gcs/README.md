# gcs - Golomb compressed set

[![Build Status](https://travis-ci.org/lontivero/gcs.svg?branch=master)](https://travis-ci.org/lontivero/gcs)

Library for creating and querying Golomb Compressed Sets (GCS), a statistical
compressed data-structure. The idea behind this implementation is using it in a new
kind of Bitcoin light client, similar to SPV clients, that uses GCS instead of bloom
filters.

This projects is based on the [original BIP (Bitcoin Improvement Proposal)](https://github.com/Roasbeef/bips/blob/master/gcs_light_client.mediawiki)
and [the Olaoluwa Osuntokun's reference implementation](https://github.com/Roasbeef/btcutil/tree/gcs/gcs)

## Privacy considerations
Using client-side filters (GCS) instead of server-side filter for a cryptocurrency light wallet improves the user
privacy given that servers cannot infer (at least no so easily) the transactions in which he is interestd on. This project
will be used as part of the [privacy-oriented HiddenWallet](https://github.com/nopara73/HiddenWallet) Bitcoin wallet project.

## How to use it


```c#
var cities = new[] { "New York", "Amsterdam", "Paris", "Buenos Aires", "La Habana" }
var citiesAsByteArrar = from city in cities select Encoding.ASCII.GetBytes(city);

// A random key
var key = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

// The false possitive rate (FPR) is calculated as:
// FPR = 1/(2**P)
var P = 16
var filter = Filter.Build(key, P, citiesAsByteArrar);

// The filter should match all ther values that were added
foreach(var name in names)
{
	Assert.IsTrue(filter.Match(name, key));
}

// The filter should NOT match any extra value
Assert.IsFalse(filter.Match(Encoding.ASCII.GetBytes("Porto Alegre"), key));
Assert.IsFalse(filter.Match(Encoding.ASCII.GetBytes("Madrid"), key));
```

## Support

### [Contributions Spent On](https://github.com/nopara73/HiddenWallet/blob/master/HiddenWallet.Documentation/DonationsSpentOn.md)

186n7me3QKajQZJnUsVsezVhVrSwyFCCZ  
[![QR Code](http://i.imgur.com/grc5fBP.png)](https://www.smartbit.com.au/address/186n7me3QKajQZJnUsVsezVhVrSwyFCCZ)

## Building From Source Code  

### Requirements:  
- [Git](https://git-scm.com/downloads)  
- [.NET Core](https://www.microsoft.com/net/core)  

### Step By Step

1. `git clone https://github.com/lontivero/gcs.git`
2. `cd gcs`  
3. `dotnet restore`  
4. `dotnet build -c Release -r win-x64`. Depending on your platform replace `win-x64` with `win-x86`, `linux-x64` or `osx-x64`.  


### Running The Tests

3. `cd tests`  
4. `dotnet restore`  
5. `dotnet build`  
6. `dotnet test`  
