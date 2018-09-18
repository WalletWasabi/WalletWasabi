SOLUTION_FOLDER=$(pwd)
PACKAGE_FOLDER=$SOLUTION_FOLDER/wasabi
BIN_FOLDER=/usr/local/bin
APP_FOLDER=/usr/share/applications
ICO_FOLDER=/usr/share/icons/hicolor/32x32/apps

PACKAGE_BIN_FOLDER=$PACKAGE_FOLDER$BIN_FOLDER/wasabi.d
PACKAGE_APP_FOLDER=$PACKAGE_FOLDER$APP_FOLDER
PACKAGE_ICO_FOLDER=$PACKAGE_FOLDER$ICO_FOLDER
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

mkdir -p $PACKAGE_BIN_FOLDER 
mkdir -p $DEBIAN_FOLDER
mkdir -p $PACKAGE_APP_FOLDER
mkdir -p $PACKAGE_ICO_FOLDER

cp WalletWasabi.Gui/Assets/WasabiLogo.png $PACKAGE_ICO_FOLDER/wasabi.png

cat <<EOT > $CONTROL_FILE
Package: WasabiWallet
Version: 1.0.0-beta
Maintainer: Adam Ficzor <adam.ficzor@gmail.com>
Architecture: all
Homepage: https://www.wasabiwallet.io/
Description: The privacy-oriented light bitcoin wallet
 is an open source wallet distributed under MIT license.
EOT

cat <<EOT > $POST_INST_FILE
#!/bin/sh
set -e
cat <<IEOT > $BIN_FOLDER/wasabi
#!/bin/sh

dotnet $INSTALLATION_FOLDER/WalletWasabi.Gui.dll
IEOT

chmod +x $BIN_FOLDER/wasabi

echo Installed!!!!
EOT


cat <<EOT > $PRE_RM_FILE
#!/bin/sh
set -e
rm -f $BIN_FOLDER/wasabi
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
