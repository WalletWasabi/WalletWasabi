# MacOS release environment guide (for signing and notarizating)

## How to create appleId and get code signing certificate

1. Get a macintosh machine. Apple developer program require two factor authentication.
2. Enroll in the apple developer program.
3. Create a New Certificate https://developer.apple.com/account/resources/certificates/list type: Developer ID Application (This certificate is used to code sign your app for distribution outside of the Mac App Store.)
4. Download the .cer file.
5. Use Keychain Access to export Personal Information Exchange (.p12) file. https://knowledge.digicert.com/solution/SO25463.html
6. Download the .p12 file to desktop and run WalletWasabi.Packager/Content/Osx/addcert.sh => Go to terminal ./addcert.sh - might run chmod a+rx addcert.sh.
7. Get application specific password: https://support.apple.com/en-us/HT204397. Use label: "notarizate".
8. Now you can use set up the environment.

## Setting up the signing environment on macOS

1. Get a macintosh machine.
2. Create a new mac user caller Release.
3. Clone WalletWasabi repository.
4. Get files from zkSNACKs safe storage: macdevsign.p12 and macsignpassw.txt. Copy them to desktop.
5. Run WalletWasabi.Packager/Content/Osx/addcert.sh => Go to terminal ./addcert.sh - might run chmod a+rx addcert.sh.
6. Install `brew install create-dmg` https://github.com/andreyvit/create-dmg

## Procedure in a few words

1. Publish with dotnet to osx.
2. Create the App folder, specify plist file.
3. Sign with entitlements.
4. Notarize.
4. Staple.
5. Create the dmg file.
6. Sign with entitlements.
7. Notarize.
8. Staple.
9. Dmg ready to distribute.

## Source

- https://github.com/zkSNACKs/WalletWasabi/pull/2886
- https://github.com/btcpayserver/BTCPayServer.Vault/blob/master/Build/travis/applesign.sh
- https://github.com/zkSNACKs/WalletWasabi/pull/928/commits/e38ed672dee25f6e45a3eb16584887cc6d48c4e6#diff-fcfcbe3692989568120c615d76ece2b2
- https://developer.apple.com/library/archive/technotes/tn2206/_index.html
- https://developer.apple.com/developer-id/
