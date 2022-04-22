#!/bin/sh
cd ~/Desktop/WalletWasabi/WalletWasabi.Packager
git pull || exit "Failed to pull git changes"
dotnet run -- sign

