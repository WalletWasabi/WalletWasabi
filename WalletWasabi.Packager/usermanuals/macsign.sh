#!/bin/sh
cd ~/Desktop/WalletWasabi/WalletWasabi.Packager
git pull
dotnet run -- sign --appleid=test@gmail.com:myapplepassword

