# Wasabi Wallet for Android

The Android app retains Wasabi's Bitcoin and WabiSabi engines and uses native
Android views, Camera2, Android Keystore and a bundled Tor process.

## Personal candidate status

Version **0.3.13 / version code 18** produces a personally signed APK. This is a
**qualification candidate, not a qualified real-funds release**. The implementation
and evidence are described in [VALIDATION.md](VALIDATION.md), with outstanding
acceptance gates in [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) and the
[source review](SECURITY_REVIEW.md).

The supported runtime selection is .NET 10 **Mono JIT**, with no trimming,
interpreter, assembly store, ReadyToRun or AOT. The minimal reproduction is in
`Contrib/Android/RuntimeProbe`. Published Bitcoin vectors and funded Bitcoin Core
acceptance pass on x86_64 emulators. The isolated Release crypto probe also
passed with the corrected source-native libraries on the connected ARM64 Android 16 phone; full handset qualification is
still outstanding. Android 7/API 24 source Release funded-wallet and public Tor
stop/restart checks pass after a scoped native handshake guard and a verified
public CA supplement. Verified native source builds retain all ELF protections
and pass strict 16 KB inspection. API 35 and API 36/16 KB source-native CI suites
and 20 consecutive fresh Release rounds have passed at their recorded source;
new changes require new evidence. Full handset qualification remains open.

New-wallet forms suggest the first unused name: First Wallet, Second Wallet,
Third Wallet and subsequent English ordinals. Recovery uses separate words and
original-password steps. Creation confirms three recovery-word positions through
six selectable words at a time. Back returns one step, preserving the original
generated words and the current form; background or inactivity locking clears
the in-memory draft. Password help uses the user's selected historical wording:
"You need BOTH the Recovery Words AND the Password to recover your wallet."

## Build and signing

Use the exact baseline in [toolchain.json](toolchain.json): SDK 10.0.401, Android
workload 36.1.69, Mono 10.0.12, JDK 21, Android platform 36 and build-tools 36.0.0.
`Contrib/Android/global.json` pins the Android SDK without roll-forward; the root
keeps the existing desktop/Nix SDK selection. `MonoRuntime.props`
pins the .NET servicing runtime through framework-reference metadata while
retaining the installed Android workload. Initial Mono 10.0.9 evidence is
identified separately in the validation record.

```powershell
dotnet workload install android@36.1.69
./Contrib/Android/prepare-tor.ps1
dotnet build WalletWasabi.Android/WalletWasabi.Android.csproj -m:1 -p:WasabiSkipBundledApps=true
```

Development packages use `io.wasabiwallet.android.dev`, default to Testnet4, and
reject mainnet below the UI, including an attempted policy-object override. Debug
instrumentation is confined to this package. Keep existing development
installations and their data intact.

A personal build uses `io.wasabiwallet.android.personal`, Release configuration,
the dedicated external key and a bootstrap JSON containing **only** `Coordinator`
and `CoordinatorIdentifier`. Desktop wallet files, passwords and RPC credentials
are not build inputs. The personal candidate copies the selected desktop
coordinator URL and identifier; CoinJoin still requires an explicit start.

On the configured Windows workstation:

```powershell
./Contrib/Android/build-personal.ps1 -CoordinatorBootstrap artifacts/android/PersonalCoordinator.json -NativeBuildDirectory artifacts/android/native-source-build/staged
```

The APK and checksum appear in
`WalletWasabi.Android/bin/Release/net10.0-android36.0/`.
The key lives outside the repository under
`~/.codex/secrets/wasabi-android-personal/`; its password uses Windows DPAPI and
restricted local access. Keep both the key and a recoverable private password
backup. Losing the signing identity prevents in-place updates. Never uninstall a
wallet or delete its data to resolve a signature error.

Recorded personal certificate SHA-256:

```text
894b7317cd04fa09534d9bd0e8d09b08bf90e9cad6718e9020f33a6fd3f7db48
```

`NativePageAlignment.targets` reuses the SDK's linker response file and adds the
[documented common-page-size flag](https://developer.android.com/guide/practices/page-sizes)
for the generated application metadata library, retaining RELRO, immediate symbol
binding and non-executable stacks. It does not patch prebuilt runtime or Tor code.

Pinned native source builds are in `Contrib/Android/NativeSources`. On Linux, after
restoring the probe with the pinned Android workload:

```sh
python3 Contrib/Android/NativeSources/build.py \
  --work /tmp/wasabi-android-native \
  --runtime-packs /usr/share/dotnet/packs \
  --output artifacts/android/native-source-build/staged
```

The recipe requires the compiler/build tools listed in `build.py`, an installed
JDK (`JAVA_HOME`) and 24 GiB free in the cache. Check the host disk too when using
a WSL VHD. Downloads go to explicit cache files and are verified before use. The
runtime receives only the pinned TLS reentrancy guard; other native code is built
from its locked source. SQLite retains the packaged version and compile options.
Tor uses stable NDK r29 and skips upstream ELF-header cleaning. Normal LLVM
stripping preserves the checked protections. SDK loader constants use the exact
hashed SDK assembly-container stub to match the unchanged packaging tools.

Select staged libraries with `-p:WasabiNativeBuildDirectory=ABSOLUTE_STAGED_PATH`.
Every selected file is hash-checked before packaging, and the APK is checked for
those bytes and compatible assembly-container layouts. Personal builds require
the verified native source directory; old prebuilt libraries do not satisfy the
strict package gate. Static checks alone do not qualify a real-funds release.

The Android workflow builds the locked native dependencies on a dedicated Linux
runner and passes that artifact to every device job. Its TLS fixture runs the
packaged Release probe through valid, untrusted and wrong-hostname connections,
including the supplemental-root path. Run it locally with `test-tls.py --serial
EMULATOR_SERIAL --apk PATH_TO_SIGNED_PROBE --openssl PATH_TO_OPENSSL`. It creates
only synthetic localhost credentials and removes its generated private key.

## Wallet operation

- Create a new wallet, recover using the original words and Wasabi password, or
  import an encrypted Wasabi JSON backup. Both BIP84 and BIP86 account public keys
  are checked at unlock. The password remains the original recovery passphrase.
- Import resets the scan height and rescans with a fresh database. Wallet saves,
  receive/change allocations and journals use atomic publication and file flushes;
  Android/Linux also flush the containing directory.
- Receive a fresh address and share a BIP21 request. Camera and payment intents use
  the same parser; scanning fills payment fields without authorizing a payment.
- Prepare a payment or replacement, review recipients, amount, fee and total, then
  authorize that operation. The unsigned PSBT remains private. Proposals expire
  after five minutes and are bound to the wallet, network and selected transaction.
- Signed bytes are journaled before broadcast. An uncertain result reserves inputs;
  restart reconciliation recognizes or rebroadcasts those same bytes. Duplicate
  confirmation returns the recorded outcome. It does not construct another payment.
- Speed-up preserves recipient amounts through RBF or eligible CPFP. Cancellation
  requires its own review and authorization and competes with confirmation of the
  original transaction. Unsupported or ineligible modifiers report unavailability.
- Device unlocking enrolls automatically after wallet creation/recovery or a
  successful password unlock on Android 11+ with supported hardware-backed
  Keystore authentication. StrongBox is preferred. Wallet selection uses the
  system prompt by default; the original wallet password remains available.
  Software-only devices retain password access. Payments, replacements, backup
  disclosure and CoinJoin start require fresh authorization. Settings contain no
  device-unlock enrollment toggle. Retired personal-node credentials are removed.
- Backgrounding and two minutes of inactivity lock the UI. Only explicitly started
  CoinJoin retains signing credentials while locked; stopping releases them.
  Device-key invalidation requests the original password and preserves the wallet.
- Public peers, filters, fees, coordinator requests and external broadcast use Tor.
  Personal-node settings and stored RPC credentials have been retired. Tor loss
  never enables direct public networking. Foreground work and wake locks remain
  subject to Android's background limits.

Cold Tor startup allows continuing descriptor/circuit progress, with a three-minute
idle limit and a ten-minute total limit. Cancellation still interrupts startup.
Compact-filter sends use bounded asynchronous waits, and timeout/disconnect cleanup
releases ranges for another peer. Partial P2P downloads preserve the reported network
target instead of presenting the downloaded height as current.

Backups and device-transfer backup are disabled. Screenshots are enabled throughout
the app at the user's request; secret fields disable autofill and personalized keyboard learning.
This cannot guarantee removal of every immutable managed string from process memory.
Hardware-wallet integration and Play Store publication are excluded.

The launcher and welcome screen use the original desktop Wasabi logo. The welcome
line is "Bitcoin. Unfairly private." Synchronization shows Tor, peer discovery,
history, filter download and wallet scanning, with a visible progress bar and
elapsed time. Remaining time is estimated only for the current download/scan stage
after measured progress; target changes, reorganizations and stalls invalidate it.
Funded regtest qualification injects its localhost node separately from settings;
that node cannot serve public-network wallets and the Android path rejects physical
devices. No node configuration or RPC credential entry is part of the mobile UI.

## Verification tools

```powershell
dotnet test --project WalletWasabi.Mobile.Tests/WalletWasabi.Mobile.Tests.csproj -p:WasabiSkipBundledApps=true --no-progress --no-ansi
./Contrib/Android/test-device.ps1 -Serial emulator-5580 -DownloadBitcoinCore
./Contrib/Android/build-personal.ps1 -CoordinatorBootstrap artifacts/android/PersonalCoordinator.json -QualificationHarness -NativeBuildDirectory artifacts/android/native-source-build/staged
./Contrib/Android/test-device.ps1 -Serial emulator-5580 -DownloadBitcoinCore -ReleaseEngine -Modes runtime,coinjoin -CoinJoinRounds 20
./Contrib/Android/test-coinjoin-failures.ps1 -Serial emulator-5580 -BitcoindPath artifacts/android/bitcoin-31.1/bin/bitcoind.exe
```

`test-device.ps1` only accepts a selected emulator, owns a new regtest directory,
verifies its pinned Bitcoin Core download, rejects occupied fixture ports, and
cleans up its own processes. Its runtime, wallet, faults, vault, Tor and CoinJoin
suites retain synthetic diagnostics and APK/source/environment verification JSON.
Records include the exact changed-file list when the checkout is modified.
Emulator-only Android builds use `packages.emulator.lock.json`; the full ARM64/x64
dependency lock remains unchanged, and both configurations support locked restore.
Optional `fees` and `public-sync` modes read actual public networks through Tor
without real-bitcoin transactions; public synchronization uses only fresh unfunded
Release-harness wallets. `-PublicNetwork main` or `testnet` qualifies either network
independently; the default `both` requires both and records the selected scope.
Public wallet synchronization logs use Info and exclude generated receive addresses.
`-ExpectedPageSize 16384` verifies execution
on a real 16 KB kernel; an AVD name alone is not evidence.

`ReleaseHarness` is a separate, emulator-only package compiled in Release with the
personal engine policy. **Never distribute it or install it on a real-funds phone.**
`NativeUiHarness` is pure Java and separately co-signed; it drives the actual personal
APK with its security flags intact. `test-release-ui.ps1` tests funded UI sending,
wrong-password rejection, duplicate taps, locking, restart and data-preserving
updates. It refuses unrelated wallets and never clears app data.
Resuming an owned fixture verifies its previous successful log hashes and archives
the record with all top-level logs before any new run can overwrite them.

`inspect-package.py` needs Python 3.9+ and `lz4==4.4.4` for qualification only. It
verifies the certificate, manifest, ZIP and ELF alignment, decompresses every
managed payload to check for test markers, compares engine payloads to SDK-prepared
build outputs, and writes source/dependency/package hashes. Failed native checks
are recorded and produce a nonzero exit; they are not silently waived.

`record-delivery.py --delivery DELIVERY_DIRECTORY --evidence-index INDEX_JSON`
binds a clean inspected APK to published source and explicitly selected public or
synthetic evidence. It copies no fixture directories or signing material and
records uncompleted handset/mainnet gates. The final package manifest and
verification record identify the exact delivered bits; earlier APK hashes do not.

The CI workflow defines API 24, 35, 36 and 36/16 KB Debug and Release matrix jobs,
plus separate 20-round and nine-scenario Release CoinJoin jobs. A configured workflow does not prove
that a remote run passed. Current local checks and limitations are recorded in
[VALIDATION.md](VALIDATION.md). Use [PHONE_HANDOFF.md](PHONE_HANDOFF.md) for the
remaining handset gate; do not use an existing funded seed as a fixture.
