#!/bin/sh

#export LC_ALL=C.UTF-8
export LANGUAGE=en_US.UTF-8
export LC_ALL=en_US.UTF-8
export LANG=en_US.UTF-8
export LC_CTYPE=en_US.UTF-8

set -e

bin_folder="bin"
version_prefix="1.1.10"
dmg_name="Wasabi"
echo "Enter apple is password:"
read apple_id_password

rm -Rf $bin_folder # Delete all.
mkdir -p $bin_folder # Use folder name bin as it is already ignored by gitignore.

dmg_folder="$bin_folder/dmg"
app_folder="$dmg_folder/Wasabi Wallet.app" # TODO: .App changed to .app, check compatibility!
publish_folder="$app_folder/Contents/MacOS"

mkdir -p "$publish_folder"

cd $bin_folder
git clone --depth 1 https://github.com/zkSNACKs/WalletWasabi
cd WalletWasabi/WalletWasabi.Gui
dotnet publish --configuration Release --force --output "../../../$publish_folder" --self-contained "true" --runtime "osx-x64" /p:VersionPrefix=1.1.10 --disable-parallel --no-cache /p:DebugType=none /p:DebugSymbols=false /p:ErrorReport=none /p:DocumentationFile="" /p:Deterministic=true

cd ../../..

cp -R "Assets/App/" "$app_folder"

#key_chain="build.keychain"
#key_chain_pass="mysecretpassword"
#security create-keychain -p "$key_chain_pass" "$key_chain"
#security default-keychain -s "$key_chain"
#security unlock-keychain -p "$key_chain_pass" "$key_chain"
#security import $HOME/Desktop/macdevsign.p12 -k "$key_chain" -P "rfrrf" -A
#CERT_IDENTITY=$(security find-identity -v -p codesigning "$key_chain" | head -1 | grep '"' | sed -e 's/[^"]*"//' -e 's/".*//')
#echo "Signing with identity $CERT_IDENTITY"
#security set-key-partition-list -S apple-tool:,apple: -s -k "$key_chain_pass" "$key_chain"

code_sign_args="--deep --force --options runtime --timestamp --entitlements Assets/entitlements.plist --sign "L233B2JQ68""

sudo codesign $code_sign_args  "$app_folder"

dmg_file="$bin_folder/$dmg_name-$version_prefix.dmg"


test -f "$dmg_file" && sudo rm "$dmg_file"
echo "File deleted"

sudo create-dmg \
  --volname "Wallet Wasabi" \
  --volicon "Assets/WasabiLogo.icns" \
  --background "Assets/Logo_with_text_small.png" \
  --window-pos 200 120 \
  --window-size 600 450 \
  --icon-size 100 \
  --icon "Wasabi Wallet.app" 100 150 \
  --hide-extension "Wasabi Wallet.app" \
  --app-drop-link 500 150 \
  --no-internet-enable \
  "$dmg_file" \
  "$app_folder"

sudo codesign $code_sign_args "$dmg_file"

codesign -dv --verbose=4 $dmg_file

bundle_id="$(cat "$app_folder/Contents/Info.plist" | grep -A1 "CFBundleIdentifier" | sed -n 's/\s*<string>\([^<]*\)<\/string>/\1/p' | xargs)"

echo "$bundle_id"

apple_id="molnardavid84@gmail.com"

sudo xcrun altool --notarize-app -t osx -f "$dmg_file" --primary-bundle-id "$bundle_id" -u "$apple_id" -p "$apple_id_password" --output-format xml | tee notarize_result
request_id="$(cat notarize_result | grep -A1 "RequestUUID" | sed -n 's/\s*<string>\([^<]*\)<\/string>/\1/p' | xargs)"
echo "Notarization in progress, request id: $request_id"
echo "Waiting for approval..."
while true; do
    echo -n "."
    sleep 10 # We need to wait 10 sec, even for the first loop because Apple might still not have their own data...
    sudo xcrun altool --notarization-info "$request_id" -u "$apple_id" -p "$apple_id_password" > notarization_progress
    if grep -q "Status: success" notarization_progress; then
        echo ""
        cat notarization_progress
        echo "Notarization succeed"
        break
    elif grep -q "Status: in progress" notarization_progress; then
        continue
    else
        cat notarization_progress
        echo "Notarization failed"
        exit 1
    fi
done

sudo xcrun stapler staple "$dmg_file"



echo "Hello world"

