# Create Remote Server

## Name
WalletWasabi.Backend.[TestNet/Main]

## Image

Ubuntu 18.04 x64

## Region

Mostly anywhere is fine, except the US or China.

## Size

https://bitcoin.org/en/full-node#minimum-requirements

[4GB Standard/32GB Standard]

# Publish

## [Remote Machine]

```
git clone https://github.com/zkSNACKs/WalletWasabi.git
cd WalletWasabi
dotnet restore
dotnet build
dotnet publish WalletWasabi.Backend --configuration Release --self-contained false
```
