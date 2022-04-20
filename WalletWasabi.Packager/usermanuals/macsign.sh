#!/bin/sh
cd ~/Desktop/WalletWasabi/WalletWasabi.Packager
git fetch
git pull
dotnet run -- sign

