# Gcs - Golomb compressed set

[![Build Status](https://travis-ci.org/lontivero/Gcs.svg?branch=master)](https://travis-ci.org/lontivero/Gcs)

Library for creating, storing and querying Golomb Compressed Sets (Gcs), a statistical
compressed data-structure. The idea behind this implementation is using it in a new
kind of Bitcoin light client, similar to SPV clients, that uses Gcs instead of bloom
filters.

This projects is based on the [original BIP (Bitcoin Improvement Proposal)](https://github.com/Roasbeef/bips/blob/master/Gcs_light_client.mediawiki)
and [the Olaoluwa Osuntokun's reference implementation](https://github.com/Roasbeef/btcutil/tree/Gcs/Gcs)

## Privacy considerations
Using client-side filters (Gcs) instead of server-side filter for a cryptocurrency light wallet improves the user
privacy given that servers cannot infer (at least no so easily) the transactions in which he is interestd on. This project
will be used as part of the [privacy-oriented HiddenWallet](https://github.com/nopara73/HiddenWallet) Bitcoin wallet project.

## How to use it


```c#
var cities = new[] { "New York", "Amsterdam", "Paris", "Buenos Aires", "La Habana" }
var citiesAsByteArray = from city in cities select Encoding.ASCII.GetBytes(city);

// A random key
var key = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

// The false possitive rate (FPR) is calculated as FPR = 1/(2**P)
var P = 16
var filter = GolombRiceFilter.Build(key, P, citiesAsByteArray);

// The filter should match all ther values that were added
foreach(var city in citiesAsByteArray)
{
	Assert.IsTrue(filter.Match(city, key));
}

// The filter should NOT match any extra value
Assert.IsFalse(filter.Match(Encoding.ASCII.GetBytes("Porto Alegre"), key));
Assert.IsFalse(filter.Match(Encoding.ASCII.GetBytes("Madrid"), key));
```

## Support

### [Contributions Spent On](https://github.com/nopara73/HiddenWallet/blob/master/HiddenWallet.Documentation/DonationsSpentOn.md)

bc1q32xe73texphk3cgu33cyw7dajky9u76qltcv6m  
[![QR Code](https://i.imgur.com/8JGnzJ7.png)](https://chainflyer.bitflyer.jp/Address/bc1q32xe73texphk3cgu33cyw7dajky9u76qltcv6m)
