#!/bin/sh

export LC_ALL=C.UTF-8
set -e

bin_folder="bin"

#rm -Rf $bin_folder # Delete all.
#mkdir -p $bin_folder # Use folder name bin as it is already ignored by gitignore.
cd $bin_folder
#git clone https://github.com/zkSNACKs/WalletWasabi

cd WalletWasabi/WalletWasabi.Gui

#dotnet publish --configuration Release --force --output "bin" --self-contained "true" --runtime "osx-x64" /p:VersionPrefix=1.1.10 --disable-parallel --no-cache /p:DebugType=none /p:DebugSymbols=false /p:ErrorReport=none /p:DocumentationFile="" /p:Deterministic=true

cd ../../.. 

dmg_folder="$bin_folder/dmg"
app_folder="$dmg_folder/Wasabi Wallet.app" # TODO: .App changed to .app, check compatibility!

mkdir -p "$app_folder/Contents/MacOS"
cp -R "$bin_folder/WalletWasabi/WalletWasabi.Gui/bin/" "$app_folder/Contents/MacOS"

cp -R "Assets/App/" "$app_folder"

cp -R "Assets/Metadata/" "$dmg_folder"

echo "Hello world"

