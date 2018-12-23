SOLUTION_FOLDER=$(pwd)
PACKAGE_FOLDER=$SOLUTION_FOLDER/wasabi
BIN_FOLDER=/usr/local/bin
APP_FOLDER=/usr/share/applications

ICON_SIZE=( 32 64 128 )
ICON_FOLDER=/usr/share/icons/hicolor
ICON_FOLDER_32=$ICON_FOLDER/32x32/apps

PACKAGE_BIN_FOLDER=$PACKAGE_FOLDER$BIN_FOLDER/wasabi.d
PACKAGE_APP_FOLDER=$PACKAGE_FOLDER$APP_FOLDER
PACKAGE_ICON_FOLDER=$PACKAGE_FOLDER$ICON_FOLDER
INSTALLATION_FOLDER=$BIN_FOLDER/wasabi.d
DEBIAN_FOLDER=$PACKAGE_FOLDER/DEBIAN
CONTROL_FILE=$DEBIAN_FOLDER/control
POST_INST_FILE=$DEBIAN_FOLDER/postinst
PRE_RM_FILE=$DEBIAN_FOLDER/prerm
DESKTOP_FILE=$PACKAGE_APP_FOLDER/wasabi.desktop

rm -rf $PACKAGE_FOLDER
rm wasabi.linux_all.deb
rm -rf $SOLUTION_FOLDER/**/bin $SOLUTION_FOLDER/**/obj

(
cd WalletWasabi.Gui
dotnet publish --force \
	--configuration Release \
	--output $PACKAGE_BIN_FOLDER \
	--runtime ubuntu.18.04-x64 
)
INSTALLATION_SIZE=$(du -sb $PACKAGE_BIN_FOLDER | cut -d$'\t' -f1)

mkdir -p $PACKAGE_BIN_FOLDER 
mkdir -p $DEBIAN_FOLDER
mkdir -p $PACKAGE_APP_FOLDER

for d in "${ICON_SIZE[@]}"
do
	mkdir -p $PACKAGE_ICON_FOLDER/$dx$d/apps
	cp WalletWasabi.Gui/Assets/wasabi-$d.png $PACKAGE_ICON_FOLDER/$dx$d/apps/wasabi.png
done

cat <<EOT > $CONTROL_FILE
Package: WasabiWallet
Version: 1.0.0-beta
Maintainer: Adam Ficsor <adam.ficsor@gmail.com>
Architecture: all
Homepage: https://www.wasabiwallet.io/
Source: https://github.com/zksnarks/WalletWasabi
Installed-Size: $INSTALLATION_SIZE
Description: Wasabi is an open-source, non-custodial, privacy focused Bitcoin wallet.
 Wasabi is an open-source, non-custodial, privacy focused Bitcoin wallet that implements trustless coin shuffling with mathematically provable anonymity, Chaumian CoinJoin, it is the first of its kind. However, "anonymity loves company", the more users there are, the better your privacy, and the faster the CoinJoin rounds will be. Whether you are looking for state of the art operational security or you are philosophically aligned with the principles of freedom and privacy, now it is YOUR time to contribute. Fire up your Wasabi and start providing liquidity for CoinJoins to bootstrap the system!
License: MIT
 Permission is hereby granted, free of charge, to any person obtaining a
 copy of this software and associated documentation files (the "Software"),
 to deal in the Software without restriction, including without limitation
 the rights to use, copy, modify, merge, publish, distribute, sublicense,
 and/or sell copies of the Software, and to permit persons to whom the
 Software is furnished to do so, subject to the following conditions:
 .
 The above copyright notice and this permission notice shall be included
 in all copies or substantial portions of the Software.
 .
 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
 OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
 CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
 TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
 SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
EOT

cat <<EOT > $POST_INST_FILE
#!/bin/sh
set -e
cat <<IEOT > $BIN_FOLDER/wasabi
#!/bin/sh

$INSTALLATION_FOLDER/WalletWasabi.Gui
IEOT

chmod +x $BIN_FOLDER/wasabi

echo Installed!!!!
EOT


cat <<EOT > $PRE_RM_FILE
#!/bin/sh
set -e
rm -f $BIN_FOLDER/wasabi
rm -f $DESKTOP_FILE
rm -f $ICON_FILE
rm -rf $INSTALLATION_FOLDER
echo Removed!!!
EOT

cat <<EOT > $DESKTOP_FILE
[Desktop Entry]
Type=Application
Name=Wasabi Wallet
Comment=Launches Wasabi Bitcoin Wallet
Icon=wasabi
Terminal=false
Exec=wasabi
Categories=wallet
EOT

chmod -R 0775 $DEBIAN_FOLDER
dpkg-deb --build $PACKAGE_FOLDER

mv $PACKAGE_FOLDER.deb wasabi.linux_all.deb
