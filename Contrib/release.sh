#!/usr/bin/env bash
#------------------------------------------------------------------------------------#
#  release.sh                                                                        #
#                                                                                    #
#  This script builds the `WasabiWallet.Fluent.Desktop` for all the supported        #
#  platforms, creates the zips and tar.gz files for all of them and creates the .deb #
#  package for Debian linux.                                                         #
#                                                                                    #
#  The automatic release process have to take these generated assets and upload them #
#  to the CI assets repository so they can be used by other jobs that can generate   #
#  and sign the installers for windows (win ci job) and generate and sign the one    #
#  for macOS (osx job).                                                              #
#------------------------------------------------------------------------------------#
set -x

STASH_MESSAGE="Stashed changes for script execution"
# Check if there are any uncommitted changes
if [[ -n $(git status --porcelain) ]]; then
  # Stash the changes
  git stash push -m "$STASH_MESSAGE"
  echo "Changes stashed."
fi

# Get the latest Git tag
LATEST_TAG=$(git describe --tags --abbrev=0)
# Extract the version number (strip the first character)
VERSION=${LATEST_TAG:1}
SHORT_VERSION=${VERSION:0:-2}

# Define project names
DESKTOP="WalletWasabi.Fluent.Desktop"
DAEMON="WalletWasabi.Daemon"
DESKTOP_PROJECT="./$DESKTOP/$DESKTOP.csproj"

# Build directory
ROOT_DIR=$(pwd)
BUILD_DIR="$ROOT_DIR/build"

# Executable name
EXECUTABLE_NAME="wassabee"

# Directory where to save the generated packages
PACKAGES_DIR="$ROOT_DIR/packages"

# Common name for all packages
PACKAGE_FILE_NAME_PREFIX="Wasabi-$VERSION"

if [[ "$RUNNER_OS" == "Windows" ]]; then
  ZIP="7z.exe a"
else
  ZIP="zip -r"
fi

if [ "$1" = "wininstaller" ]; then
  # Supported platforms
  PLATFORMS=("win-x64")
  CREATE_WINDOWS_INSTALLER="yes"
  CREATE_DEBIAN_PACKAGE="no"
  RELEASE_NOTE="no"
  SIGN_PGP="no"
  CREATE_OSX_DMG="no"
elif [ "$1" = "debian" ]; then
  # Supported platforms
  PLATFORMS=("linux-x64" "osx-x64" "osx-arm64")
  CREATE_WINDOWS_INSTALLER="no"
  CREATE_DEBIAN_PACKAGE="yes"
  RELEASE_NOTE="no"
  SIGN_PGP="no"
  CREATE_OSX_DMG="no"
elif [ "$1" = "dmg" ]; then
  PLATFORMS=("osx-x64")
  CREATE_WINDOWS_INSTALLER="no"
  CREATE_DEBIAN_PACKAGE="no"
  RELEASE_NOTE="no"
  SIGN_PGP="no"
  CREATE_OSX_DMG="yes"
elif [ "$1" = "releasenote" ]; then
  PLATFORMS=()
  CREATE_WINDOWS_INSTALLER="no"
  CREATE_DEBIAN_PACKAGE="no"
  RELEASE_NOTE="yes"
  SIGN_PGP="no"
  CREATE_OSX_DMG="no"
elif [ "$1" = "gpgsign" ]; then
  PLATFORMS=()
  CREATE_WINDOWS_INSTALLER="no"
  CREATE_DEBIAN_PACKAGE="no"
  RELEASE_NOTE="no"
  SIGN_PGP="yes"
  CREATE_OSX_DMG="no"
fi

# Remove the build directory if it exists and recreate it
# rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# Create packages directory (where all final packages are saved)
#rm -rf "$PACKAGES_DIR" &&
mkdir -p "$PACKAGES_DIR"

#------------------------------------------------------------------------------------#
# BUILD DESKTOP FOR ALL PLATFORMS                                                    #
#------------------------------------------------------------------------------------#
# Loop through each platform and build the project
for PLATFORM in "${PLATFORMS[@]}"; do
  # Define output directory for the platform
  OUTPUT_DIR=$BUILD_DIR/$PLATFORM

  # Build dotnet application
  dotnet restore $DESKTOP_PROJECT --locked-mode
  dotnet publish $DESKTOP_PROJECT \
          --configuration Release \
          --runtime $PLATFORM \
          --force \
          --output $OUTPUT_DIR \
          --self-contained true \
          --disable-parallel \
          --no-cache \
          --no-restore \
          --property:SelfContained=true \
          --property:VersionPrefix=$VERSION \
          --property:DebugType=none \
          --property:DebugSymbols=false \
          --property:ErrorReport=none \
          --property:DocumentationFile='' \
          --property:Deterministic=true \
          /clp:ErrorsOnly

  # Determine executable file extension based on platform
  EXE_FILE_EXTENSION=''
  PLATFORM_PREFIX="${PLATFORM:0:3}"
  if [[ "$PLATFORM_PREFIX" == "win" ]]; then
    EXE_FILE_EXTENSION=".exe"
  fi

  # Rename executables as wassabee and wassabeed
  mv $OUTPUT_DIR/{$DESKTOP,${EXECUTABLE_NAME}}$EXE_FILE_EXTENSION
  mv $OUTPUT_DIR/{$DAEMON,${EXECUTABLE_NAME}d}$EXE_FILE_EXTENSION

  # Remove microservices binaries for other platforms
  MICRO_SERVICES_DIR="$OUTPUT_DIR/Microservices/Binaries"
  export PLATFORM_MICRO_SERVICES="${PLATFORM:0:3}${PLATFORM: -2}"
  find $MICRO_SERVICES_DIR -mindepth 1 -maxdepth 1 -type d ! -name "$PLATFORM_MICRO_SERVICES" -exec rm -rf {} +

  # Hack! *.deps.json files contains this SHA516 that depends on the absolute path of
  # the nuget packages. This means that these files are different in different computers
  # and for different users. (End goal: reproducibility)
  sed -i 's/"sha512": "sha512-[^"]*"/"sha512": ""/g' "$OUTPUT_DIR/$DESKTOP.deps.json"

  # Adjust platform name for macOS
  ALTER_PLATFORM=$PLATFORM
  if [[ "${PLATFORM_PREFIX}" == "osx" ]]; then
    ALTER_PLATFORM="macOS${PLATFORM:3}"
  fi

  # Create compressed package files (.zip and .tar.gz)
  PACKAGE_FILE_NAME=$PACKAGE_FILE_NAME_PREFIX-$ALTER_PLATFORM
  if [[ "${PLATFORM_PREFIX}" == "lin" ]]; then
    tar -pczvf $PACKAGES_DIR/$PACKAGE_FILE_NAME.tar.gz $OUTPUT_DIR
  else
    pushd "$OUTPUT_DIR" || exit
    $ZIP "$PACKAGES_DIR/$PACKAGE_FILE_NAME.zip" .
    popd || exit
  fi
done


#------------------------------------------------------------------------------------#
# CREATE DEBIAN PACKAGE                                                              #
#------------------------------------------------------------------------------------#
if [ "$CREATE_DEBIAN_PACKAGE" = "yes" ]; then
# Create .deb package
DEBIAN_PACKAGE_DIR=$BUILD_DIR/deb
DEBIAN=$DEBIAN_PACKAGE_DIR/DEBIAN
DEBIAN_USR=$DEBIAN_PACKAGE_DIR/usr
DEBIAN_BIN=$DEBIAN_USR/local/bin

# Create necessary directories
mkdir -p $DEBIAN
mkdir -p $DEBIAN_BIN
mkdir -p $DEBIAN_USR/share/{applications,icons/hicolor}

# Copy icon files
for ICON_FILE in "$DESKTOP"/Assets/WasabiLogo*.png; do
  SIZE=$(echo "$ICON_FILE" | grep -oP '\d+')
  ICON_DIR="$DEBIAN_USR/share/icons/hicolor/${SIZE}x${SIZE}/app"
  mkdir -p "$ICON_DIR"
  cp "$ICON_FILE" "$ICON_DIR/$EXECUTABLE_NAME.png"
done

# Calculate package size (in kilobytes)
DEBIAN_PACKAGE_SIZE=$(du -s $BUILD_DIR/linux-x64 | cut -f1)

# Create the control file content
DEBIAN_CONTROL_FILE_CONTENT="Package: ${EXECUTABLE_NAME}
Priority: optional
Section: utils
Maintainer: Wasabi Wallet Team
Version: ${VERSION}
Homepage: https://wasabiwallet.io
Vcs-Git: git://github.com/WalletWasabi/WalletWasabi.git
Vcs-Browser: https://github.com/WalletWasabi/WalletWasabi
Architecture: amd64
License: Open Source (MIT)
Installed-Size: ${DEBIAN_PACKAGE_SIZE}
Recommends: policykit-1
Description: open-source, non-custodial, privacy focused Bitcoin wallet
  Built-in Tor, coinjoin, payjoin and coin control features."

echo "${DEBIAN_CONTROL_FILE_CONTENT}" > $DEBIAN/control

# Post-installation script content
USR_LOCAL_BIN_DIR="/usr/local/bin"
INSTALL_DIR="${USR_LOCAL_BIN_DIR}/wasabiwallet"
DEBIAN_POST_INST_SCRIPT_CONTENT="#!/usr/bin/env sh
${INSTALL_DIR}/Microservices/Binaries/lin64/hwi installudevrules
exit 0"
echo "${DEBIAN_POST_INST_SCRIPT_CONTENT}" > $DEBIAN/postinst
chmod 0775 ${DEBIAN}/postinst

# Create the desktop file content
DEBIAN_DESKTOP_CONTENT="[Desktop Entry]
Type=Application
Name=Wasabi Wallet
StartupWMClass=Wasabi Wallet
GenericName=Bitcoin Wallet
Comment=Privacy focused Bitcoin wallet.
Icon=${EXECUTABLE_NAME}
Terminal=false
Exec=${EXECUTABLE_NAME}
Categories=Office;Finance;
Keywords=bitcoin;wallet;crypto;blockchain;wasabi;privacy;anon;awesome;"

# Write the content to the file
DEBIAN_DESKTOP="${DEBIAN_USR}/share/applications/${EXECUTABLE_NAME}.desktop"
echo "${DEBIAN_DESKTOP_CONTENT}" > $DEBIAN_DESKTOP
sudo chmod 0644 $DEBIAN_DESKTOP

# Copy the build to into the debian package structure
cp -r $BUILD_DIR/linux-x64 $DEBIAN_BIN/wasabiwallet

# Create wrapper scripts
echo "#!/usr/bin/env sh
${INSTALL_DIR}/${EXECUTABLE_NAME} \$@" > ${DEBIAN_BIN}/${EXECUTABLE_NAME}

echo "#!/usr/bin/env sh
${INSTALL_DIR}/${EXECUTABLE_NAME}d \$@" > ${DEBIAN_BIN}/${EXECUTABLE_NAME}d

# Remove execution to everything except for executables and their wrapper scripts
sudo chmod -R 0644 ${DEBIAN_BIN}/wasabiwallet
sudo chmod 0775 ${DEBIAN_BIN}/wasabiwallet/${EXECUTABLE_NAME}{,d}
sudo chmod 0775 ${DEBIAN_BIN}/${EXECUTABLE_NAME}{,d}

# Build the .deb package
dpkg --build "${DEBIAN_PACKAGE_DIR}" "$PACKAGES_DIR/${PACKAGE_FILE_NAME_PREFIX}.deb"
fi

#------------------------------------------------------------------------------------#
# CREATE WINDOWS INSTALLER                                                           #
#------------------------------------------------------------------------------------#
if [ "$CREATE_WINDOWS_INSTALLER" = "yes" ]; then
WIX_BIN_DIR="$WIX/bin"
WINDOWS_INSTALLER_DIR="WalletWasabi.WindowsInstaller"
CANDLE_EXE="$WIX_BIN_DIR/"candle
LIGHT_EXE="$WIX_BIN_DIR"/light
HEAT_EXE="$WIX_BIN_DIR"/heat
BUILD_INSTALLER_DIR="$BUILD_DIR/win-installer"
WINDOWS_PACKAGE_BUILD_DIR="$BUILD_DIR"/win-x64

mkdir -p "$BUILD_INSTALLER_DIR"
"$HEAT_EXE" dir $WINDOWS_PACKAGE_BUILD_DIR \
    -o $WINDOWS_INSTALLER_DIR/ComponentsGenerated.wxs \
    -cg PublishedComponents \
    -pog Binaries \
    -dr INSTALLFOLDER \
    -srd -scom -gg -sfrag -sreg

# Compile the .wxs file to .wixobj
"$CANDLE_EXE" \
    $WINDOWS_INSTALLER_DIR/*.wxs \
    -arch x64 \
    -dBuildVersion=$VERSION \
    -dDesktopProjectDir=$DESKTOP \

# Link the .wixobj file to create the .msi installer
"$LIGHT_EXE" \
    *.wixobj \
    -loc $WINDOWS_INSTALLER_DIR/Common.wxl \
    -ext "$WIX_BIN_DIR/WixUIExtension.dll" \
    -ext "$WIX_BIN_DIR/WixUtilExtension.dll" \
    -b $WINDOWS_PACKAGE_BUILD_DIR \
    -out $PACKAGES_DIR/$PACKAGE_FILE_NAME_PREFIX.msi

# Remove unwanted file
rm $PACKAGES_DIR/*.wixpdb

# Sign the installer
signtool sign /d "Wasabi Wallet" /f Certificate.pfx  /p "$SIGNING_CERTIFICATE_PASSWORD" /t http://timestamp.digicert.com /v "$PACKAGES_DIR/$PACKAGE_FILE_NAME_PREFIX.msi"
fi

#------------------------------------------------------------------------------------#
# CREATE OSX .DMG                                                                    #
#------------------------------------------------------------------------------------#
if [ "$CREATE_OSX_DMG" = "yes" ]; then

for OSX_ZIP_PACKAGE in $PACKAGES_DIR/Wasabi*macOS*.zip; do
# Combine paths
ZIP_PACKAGE=$(basename "$OSX_ZIP_PACKAGE")
OSX_BUILD_DIR="$BUILD_DIR/$ZIP_PACKAGE/osx"
DMG_PATH="$OSX_BUILD_DIR/dmg"
APP_NAME="Wasabi Wallet.app"
APP_PATH="$DMG_PATH/$APP_NAME"
APP_CONTENTS_PATH="$APP_PATH/Contents"
APP_MACOS_PATH="$APP_CONTENTS_PATH/MacOS"
APP_RES_PATH="$APP_CONTENTS_PATH/Resources"
INFO_FILE_PATH="$APP_CONTENTS_PATH/Info.plist"
APP_NOTARIZE_FILE_PATH="$OSX_ZIP_PACKAGE"

mkdir -p "$APP_RES_PATH"

unzip "$PACKAGES_DIR/$PACKAGE_FILE_NAME_PREFIX-macOS-x64.zip" -d "$APP_MACOS_PATH"

echo "<?xml version=\"1.0\" encoding=\"UTF-8\"?>
<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">
<plist version=\"1.0\">
<dict>
	<key>LSArchitecturePriority</key>
	<array>
		<string>x86_64</string>
	</array>
	<key>CFBundleIconFile</key>
	<string>WasabiLogo.icns</string>
	<key>CFBundlePackageType</key>
	<string>APPL</string>
	<key>CFBundleShortVersionString</key>
	<string>$SHORT_VERSION</string>
	<key>CFBundleVersion</key>
	<string>$SHORT_VERSION</string>
	<key>CFBundleExecutable</key>
	<string>wassabee</string>
	<key>CFBundleName</key>
	<string>Wasabi Wallet</string>
	<key>CFBundleIdentifier</key>
	<string>zksnacks.wasabiwallet</string>
	<key>NSHighResolutionCapable</key>
	<true/>
	<key>NSAppleScriptEnabled</key>
	<true/>
	<key>LSApplicationCategoryType</key>
	<string>public.app-category.finance</string>
	<key>CFBundleInfoDictionaryVersion</key>
	<string>6.0</string>
</dict>
</plist>" > "$INFO_FILE_PATH"

mkdir -p $DMG_PATH/.fseventsd/
echo 'H4sIAAAAAAAAEzIK9nHZ4u1R/ZkBCN6vbgRRDC4MDIxMDFDQt6oRxoSL6bkExweX5BelMhhAtYQy
NDBsY4TKhuXnlOameibn5+llJucVM2yHGhHK3MCwFaYoKTE5O70ovzQvhaEKKt8AtHcDpry+T356
fnx5ZklGfElqRUl8cW5iTo5eQV46Qw9UoyBjA8NGqEbHgoKczOTEksx8oM0ToAoYGRwYNkEVAAAA
AP//AwDwiBgo8wAAAA==' | base64 -d > "$DMG_PATH/.fseventsd/000000000081abf0"
echo 'H4sIAAAAAAAAEzIM9nGpVFt51IqBgUEvrTi1LDWvpDhFvzhHNzk/tyCxhOH76kYGKGBk+IHgKAIA
AAD//wMAmAcQvToAAAA=' | base64 -d > "$DMG_PATH/.fseventsd/000000000081abf1"
echo '5D4F6D41-8967-4D1E-9953-35A263D5EFDF' > "$DMG_PATH/.fseventsd/fseventsd-uuid"

# Give read/write to owner, read to group and others and, remove write to group and others
sudo chmod -R u+rwX,go+rX,go-w "$APP_PATH"

ENTITLEMENTS_PATH="$OSX_BUILD_DIR/entitlements.plist"
echo '
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>com.apple.security.cs.allow-jit</key>
	<true/>
	<key>com.apple.security.cs.allow-unsigned-executable-memory</key>
	<true/>
	<key>com.apple.security.cs.allow-dyld-environment-variables</key>
	<true/>
	<key>com.apple.security.cs.disable-library-validation</key>
	<true/>
</dict>
</plist>' > "$ENTITLEMENTS_PATH"

# Separate files in wasabi executables, non-wasabi executables (tor, bitcoin and so on) and the rest
NON_EXECUTABLES=()
OTHER_EXECUTABLES=()
WASABI_EXECUTABLES=()
while IFS= read -r -d '' file; do
  # Check if the file is a Mach-O executable
  if file "$file" | grep -q 'Mach-O.* executable'; then
    if [[ "$file" == *"wassabee"* ]]; then
      WASABI_EXECUTABLES+=("$file")
    else
      OTHER_EXECUTABLES+=("$file")
    fi
  else
    NON_EXECUTABLES+=("$file")
  fi
done < <(find "$APP_PATH" -type f -print0)

EXECUTABLES=("${OTHER_EXECUTABLES[@]}" "${WASABI_EXECUTABLES[@]}")
sudo chmod u+x "${EXECUTABLES[@]}"

# Signing all files in order (wassabee at the end)
SIGN_ARGUMENTS="--sign \"L233B2JQ68\" --verbose --force --options runtime --timestamp ---entitlements $ENTITLEMENTS_PATH"
ALL_FILES=("${NON_EXECUTABLES[@]}" "${EXECUTABLES[@]}")
for file in "${ALL_FILES[@]}"; do
  codesign "$SIGN_ARGUMENTS" "$file"
done

# Notarization
ditto -c -k --keepParent "$APP_PATH" "$APP_NOTARIZE_FILE_PATH"

xcrun notarytool submit --wait --apple-id "$APPLE_ID" -p "WasabiNotarize" "$APP_NOTARIZE_FILE_PATH"
done
fi

#------------------------------------------------------------------------------------#
# SIGN EVERYTHING                                                                    #
#------------------------------------------------------------------------------------#
if [ "$SIGN_PGP" = "yes" ]; then
pushd "$PACKAGES_DIR" || exit
for FILE in ./*; do
  sha256sum "$FILE" >> SHA256SUMS
  gpg --armor --detach-sign --output "$FILE.asc" "$FILE"
done
gpg --sign --digest-algo sha256 -a --clearsign --armor --output SHA256SUMS.asc SHA256SUMS

echo "
#r \"nuget:NBitcoin\"
open System
open System.IO
open System.Security.Cryptography
open NBitcoin

let args = Environment.GetCommandLineArgs()
let wasabiPrivateKey = Key.Parse(args[3], Network.Main)
args[2]
|> File.ReadAllBytes
|> SHA256.HashData
|> uint256
|> wasabiPrivateKey.Sign
|> _.ToDER()
|> Convert.ToBase64String
|> Console.WriteLine
" > signer.fsx
dotnet fsi signer.fsx SHA256SUMS.asc $SIGNING_WASABI_KEY > SHA256SUMS.wasabisig
rm signer.fsx

popd || exit
fi

if [ "$RELEASE_NOTE" = "yes" ]; then
  sed -e "s/{version}/$VERSION/g" \
      -e "/{highlights}/r ./Contrib/ReleaseHighlight.md" \
      -e "/{highlights}/d" \
      ./Contrib/ReleaseTemplate.md
fi

# Unstash changes if there were any
if git stash list | head -1 | grep -q "$STASH_MESSAGE"; then
  git stash pop
  echo "Changes unstashed."
fi
