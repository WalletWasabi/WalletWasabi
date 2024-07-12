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

# Define project names
DESKTOP="WalletWasabi.Fluent.Desktop"
DAEMON="WalletWasabi.Daemon"
DESKTOP_PROJECT="./$DESKTOP/$DESKTOP.csproj"

# Build directory
BUILD_DIR="./build"

# Executable name
EXECUTABLE_NAME="wassabee"

# Directory where to save the generated packages
PACKAGES_DIR="packages"

# Common name for all packages
PACKAGE_FILE_NAME_PREFIX="Wasabi-$VERSION"

# Supported platforms
PLATFORMS=("win-x64" "linux-x64" "osx-x64" "osx-arm64")

# Remove the build directory if it exists
rm -rf $BUILD_DIR

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
          --property:VersionPrefix=$VERSION \
          --property:DebugType=none \
          --property:DebugSymbols=false \
          --property:ErrorReport=none \
          --property:DocumentationFile='' \
          --property:Deterministic=true

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
  mkdir -p "$PACKAGES_DIR"
  PACKAGE_FILE_NAME=$PACKAGE_FILE_NAME_PREFIX-$ALTER_PLATFORM
  if [[ "${PLATFORM_PREFIX}" == "lin" ]]; then
    tar -pczvf $PACKAGES_DIR/$PACKAGE_FILE_NAME.tar.gz $OUTPUT_DIR
  else
    zip -r $PACKAGES_DIR/$PACKAGE_FILE_NAME.zip $OUTPUT_DIR
  fi
done

#------------------------------------------------------------------------------------#
# CREATE DEBIAN PACKAGE                                                              #
#------------------------------------------------------------------------------------#
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
#chmod 0644 $DEBIAN_DESKTOP

# Copy the build to into the debian package structure
cp -r $BUILD_DIR/linux-x64 $DEBIAN_BIN/wasabiwallet

# Create wrapper scripts
echo "#!/usr/bin/env sh
${INSTALL_DIR}/${EXECUTABLE_NAME} \$@" > ${DEBIAN_BIN}/${EXECUTABLE_NAME}

echo "#!/usr/bin/env sh
${INSTALL_DIR}/${EXECUTABLE_NAME}d \$@" > ${DEBIAN_BIN}/${EXECUTABLE_NAME}d

# Remove execution to everything except for executables and their wrapper scripts
chmod -R 0644 ${DEBIAN_BIN}/wasabiwallet
chmod 0775 ${DEBIAN_BIN}/wasabiwallet/${EXECUTABLE_NAME}{,d}
chmod 0775 ${DEBIAN_BIN}/${EXECUTABLE_NAME}{,d}

# Build the .deb package
dpkg --build "${DEBIAN_PACKAGE_DIR}" "$PACKAGES_DIR/${PACKAGE_FILE_NAME_PREFIX}.deb"


# Unstash changes if there were any
if git stash list | head -1 | grep -q "$STASH_MESSAGE"; then
  git stash pop
  echo "Changes unstashed."
fi
