SOLUTION_FOLDER=$(pwd)
PACKAGE_FOLDER=$SOLUTION_FOLDER/wasabi
BIN_FOLDER=/usr/local/bin
PACKAGE_BIN_FOLDER=$PACKAGE_FOLDER$BIN_FOLDER/wasabi.d
INSTALLATION_FOLDER=$BIN_FOLDER/wasabi.d
DEBIAN_FOLDER=$PACKAGE_FOLDER/DEBIAN
CONTROL_FILE=$DEBIAN_FOLDER/control
POST_INST_FILE=$DEBIAN_FOLDER/postinst
PRE_RM_FILE=$DEBIAN_FOLDER/prerm


rm -rf $PACKAGE_FOLDER
rm wasabi.linux_all.deb
rm -rf $SOLUTION_FOLDER/**/bin $SOLUTION_FOLDER/**/obj

(
cd WalletWasabi.Gui
dotnet publish --force \
	--configuration Release \
	--output $PACKAGE_BIN_FOLDER
#	--runtime ubuntu.18.04-x64 \
)

mkdir -p $PACKAGE_BIN_FOLDER $DEBIAN_FOLDER

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

chmod -R 0775 $DEBIAN_FOLDER
dpkg-deb --build $PACKAGE_FOLDER

mv $PACKAGE_FOLDER.deb wasabi.linux_all.deb
