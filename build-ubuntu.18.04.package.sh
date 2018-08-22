FOLDER=wasabi

dotnet clean
rm -rf $FOLDER
cd WalletWasabi.Gui
dotnet publish -c Release -r ubuntu.18.04-x64 --self-contained
cd ..

mkdir $FOLDER && mkdir $FOLDER/DEBIAN
mkdir -p $FOLDER/usr/local/bin
cp -r WalletWasabi.Gui/bin/Release/netcoreapp2.1/ubuntu.18.04-x64/publish/. $FOLDER/usr/local/bin/wasabiwallet.d/

cat <<EOT >> $FOLDER/DEBIAN/control
Package: WasabiWallet
Version: 1.0.0
Maintainer: Adam Ficzor <adam.ficzor@gmail.com>
Architecture: all
Depends: libc6, libgcc1, libgssapi-krb5-2, liblttng-ust0, libssl0.9.8 | libssl1.0.0 | libssl1.0.1 | libssl1.0.2, libstdc++6, libunwind8, libuuid1, zlib1g
Description: The privacy-oriented light bitcoin wallet
 is an open source wallet distributed under MIT license.
EOT

cat <<EOT >> $FOLDER/DEBIAN/postinst
#!/bin/sh
set -e
cat <<IEOT >> /usr/local/bin/wasabi 
#!/bin/sh

dotnet /usr/local/bin/wasabiwallet.d/WalletWasabi.Gui.dll
IEOT

chmod +x /usr/local/bin/wasabi

echo Installed!!!!
EOT


cat <<EOT >> $FOLDER/DEBIAN/prerm

#!/bin/sh
set -e
rm /ust/local/bin/wasabi
echo Removed!!!
EOT

find ./$FOLDER/DEBIAN -type f | xargs chmod 755

dpkg-deb --build $FOLDER

mv $FOLDER.deb wasabi.linux_all.deb

