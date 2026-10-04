# Wasabi Wallet for Android

The Android application uses native Android views and the existing Wasabi Bitcoin,
storage, compact-filter, transaction, and WabiSabi engines. It is a separate application
project; building the desktop solution does not require the Android workload.

## Release status

Version 0.1.0 is a development build. The APK supports Android 7.0 / API 24 and later
on arm64 and x86_64. Its runtime is .NET 10 CoreCLR for Android, which Microsoft
[labels experimental](https://learn.microsoft.com/en-us/dotnet/maui/deployment/runtimes-compilation?view=net-maui-10.0).
The Mono runtime could not execute the existing NBitcoin engine correctly during
device tests, so this port uses CoreCLR without changing the cryptographic engine.

Emulator regtest validation does not establish production readiness, independent
security audit, physical ARM device compatibility, camera compatibility, or store
approval. Hardware-wallet signing, the desktop RPC server, Scheme console, and
desktop installer updates are not exposed by the Android interface. No hardware
wallet is impersonated by importing a watch-only file.

## Build

Install .NET SDK 10, its Android workload, JDK 21, Android SDK platform 36, and
build tools 36.0.0. Run these commands from the repository root in PowerShell 7:

```powershell
dotnet workload install android
./Contrib/Android/prepare-tor.ps1
dotnet build WalletWasabi.Android/WalletWasabi.Android.csproj -c Release -p:WasabiSkipBundledApps=true
```

Set `JavaSdkDirectory` and `AndroidSdkDirectory` if the tools are not discoverable.
The output is
`WalletWasabi.Android/bin/Release/net10.0-android36.0/io.wasabiwallet.android-Signed.apk`.
Local and CI builds use a development signing key unless a release key is supplied.
Keep the signing key stable between releases. For distribution, provide the usual
.NET Android `AndroidKeyStore`, `AndroidSigningKeyStore`, `AndroidSigningKeyAlias`,
`AndroidSigningStorePass`, and `AndroidSigningKeyPass` properties through secure CI
configuration. Do not put signing passwords in source control.

The Tor preparation script downloads a pinned Guardian Project Android Tor AAR,
verifies its SHA-256, and extracts only arm64 and x86_64 executable libraries.
The exact version, source URL, and hash are in
`WalletWasabi.Android/Native/tor.lock.json`. Native artifacts are ignored by Git.
An ordinary desktop Tor binary cannot run on Android.

## Wallet behavior

- Create a wallet, confirm its recovery words, and keep the original password with
  them. Wasabi's password is also the BIP39 recovery passphrase; a different one
  derives a different wallet.
- Recover from a valid BIP39 mnemonic, or import an existing encrypted Wasabi JSON
  wallet. Imports rescan from the earliest supported checkpoint because a JSON file
  does not contain the transaction database. Scan time depends on wallet age and
  network connectivity. Balances stay undisplayed until the wallet finishes loading.
- Receive through a fresh Taproot address and BIP21 QR request; copy the address or
  share the payment request. Scan payment QR codes with the camera to prepare sends.
- Select coins, set a fee rate, review the exact destination, amount, and fee, then
  confirm the password. Signing uses the reviewed PSBT; expired, superseded, altered,
  unsynchronized, or unavailable-input reviews are rejected.
- View transaction history, individual outputs, confirmation status, coins, labels,
  anonymity sets, and privacy percentage. Exclude individual coins from CoinJoin.
- Configure the coordinator URL and identifier supplied by its operator to use
  CoinJoin. There is no implicit third-party coordinator. Stopping waits for the
  CoinJoin manager to release the wallet safely before clearing signing keys.
- Mainnet, Testnet4, and Signet keep separate wallets and databases. Regtest is used
  by the emulator fixture rather than offered as a consumer network choice.
- Export an encrypted wallet file through Android's document picker and verify
  recovery words against the complete account public keys. Recovery words are not persisted.

All non-regtest network connections use the bundled Tor process. A personal Bitcoin
RPC node may be configured only at loopback or an onion address; onion RPC requests
use Tor. The Android foreground service keeps synchronization and active CoinJoin
running while Android allows it. Work-related wake locks have a bounded lifetime.
Android may stop the process, restrict background operation, or enforce the data-sync
foreground-service time limit; the wallet reloads and resumes scanning on reopening.

The interface locks on backgrounding and inactivity. Signing keys remain available
to an active CoinJoin until it stops safely. Cloud backup and device-transfer backup
are disabled. Release screens block screenshots, passwords disable autofill and
personalized keyboard learning, and clipboard items are marked sensitive where the
Android version supports it. These protections do not replace a device security audit.

## Verification

Host-side tests:

```powershell
dotnet test --project WalletWasabi.Mobile.Tests/WalletWasabi.Mobile.Tests.csproj -p:WasabiSkipBundledApps=true --no-progress --no-ansi
```

For device tests, build the Debug APK and start a dedicated Android API 35 x86_64
emulator. No physical phone or user wallet is used by the fixture:

```powershell
dotnet build WalletWasabi.Android/WalletWasabi.Android.csproj -p:WasabiSkipBundledApps=true -p:WasabiEmulatorOnly=true
./Contrib/Android/test-device.ps1 -Serial emulator-5554 -DownloadBitcoinCore
```

The script verifies a pinned Bitcoin Core download, creates a fresh private regtest
data directory, mines test coins, launches the actual Wasabi coordinator with a
two-input test round, launches a second independent wallet participant, configures
ADB reverse tunnels, and installs the Debug APK.
It rejects occupied fixture ports and shuts down only its own node and coordinator.
Logs are written under `artifacts/android/device-<id>/`.

Three independent on-device suites must report an explicit `PASS`:

1. **wallet**: funded Taproot receive, synchronization, stale-review and wrong-password
   rejection, signing, Bitcoin Core broadcast acceptance, mining confirmation,
   history, lock, persistence, seed recovery and rescan, managed WabiSabi credentials,
   and QR encoding/decoding.
2. **coinjoin**: two funded participants with independent keys, the real coordinator's complete protocol round,
   Bitcoin Core broadcast acceptance, confirmation, safe stop, and key clearing.
   Solo-CoinJoin protection remains enabled. Regtest does not measure real-world anonymity.
3. **tor**: executable startup, full bootstrap, SOCKS negotiation, and a public TLS
   request whose destination verifies that it arrived from a Tor exit.

Instrumentation is compiled only into Debug packages. `WasabiUiTest=true` additionally
allows screenshots for visual QA and is rejected for Release builds. Neither setting
should be enabled for a distributed package. QR codec tests do not validate a physical
camera. Test credentials and recovery words are public and used only for regtest.

The Android GitHub workflow builds both architectures, runs host tests and the device
fixture, and uploads the development-signed APK. A configured workflow is not evidence
that a particular remote CI run succeeded; inspect the run's result and logs.

## Code organization

`WalletWasabi.Mobile` owns wallet operations and safety checks. `WalletWasabi.Android`
owns native screens, camera scanning, the foreground service, Tor process lifetime,
and app-private storage. `WalletWasabi.Android.slnx` opens just the mobile projects
and shared engine. `Contrib/Android/RegtestCoordinator` is a test fixture referencing
the real coordinator, not a simulated CoinJoin implementation.

The shared engine changes allow an explicit resource base directory, an Android Tor
executable path, disabling desktop file bundling for mobile builds, clearing cached
signing secrets on lock, and disposing/recreating named workers safely. Desktop
defaults and normal desktop bundling remain available.
