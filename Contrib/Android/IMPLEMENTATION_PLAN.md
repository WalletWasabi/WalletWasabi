# Android wallet implementation plan for personal use

This plan covers the work needed to turn the existing Android development build into
a tested software Bitcoin wallet for your phone: create and recover a wallet, receive
and send bitcoin, manage fees and transaction history, retain Wasabi's Tor transport,
and use CoinJoin with a configured coordinator. The implementation and automated
verification below are work I can perform myself. Tests on your actual phone require
access to that phone; keeping your recovery backup and authorizing a real bitcoin
payment remain your responsibility.

The intended deliverable is a release APK with a stable signing identity, a repeatable
update path, and evidence tied to its exact source and package. This plan is not a claim
that the current APK is ready for real funds. My code review and tests also cannot be
described as an independent security audit or a guarantee against loss.

## Current baseline

Reviewed on 2026-10-04 at commit `764b0ecab696b7427f5625135f2a9b1297d4bc4d`.
[VALIDATION.md](VALIDATION.md) records the existing evidence: 29 mobile tests,
49 shared-engine tests, successful emulator receive/send/recovery, a complete
two-participant regtest CoinJoin, Tor connectivity, and an installed Release APK.
Physical ARM64 operation and a payment using real mainnet bitcoin remain unverified.
An earlier CoinJoin run failed during output registration without a verified cause.

The source exposes additional work that a successful build does not resolve:

- `WalletWasabi.Android.csproj` selects experimental .NET 10 Android CoreCLR.
- `MobileSettings.Network` defaults to mainnet; there is no development-build policy
  preventing mainnet wallet creation or spending.
- `MobileSettings.Save` writes Bitcoin RPC credentials to ordinary JSON.
- `WalletSession.SendAsync` has no durable record for reconciling an interrupted or
  ambiguous broadcast.
- `ScannerActivity` uses the legacy camera API and fixes preview orientation to 90 degrees.
- `WalletService` needs systematic interruption, background-budget, and restart testing.
- Android exposes no transaction speed-up or cancellation flow, although the shared
  engine provides those operations.
- Local and CI packaging use development certificates; CI does not establish a stable
  identity for future updates.

No Android device is currently connected through the checked ADB installations.

## Work I can complete myself

The workstreams are in implementation order. All are planned, rather than completed.
Each includes the code boundary and acceptance evidence. Shared-engine changes must
retain the desktop behavior and pass the relevant desktop checks.

### 1 Establish development and release policies

**Code:** `WalletWasabi.Android.csproj`, `MainActivity.cs`, `WalletSession.cs`,
`MobileSettings.cs`, `WalletInstrumentation.cs`, and `.github/workflows/android.yml`.

- Separate development packages and data from the personal release package.
- Default development builds to Testnet4 and enforce their mainnet restriction in
  wallet operations, including create, recovery, import, unlock for signing, CoinJoin,
  and broadcast. A UI setting alone must not bypass the restriction.
- Keep regtest instrumentation, public fixture credentials, and screenshot exemptions
  confined to test packages. Retain screenshot protection in release verification.
- Make release packaging reject development signing, missing release metadata, or
  enabled test hooks. Keep a record of unfinished release gates.

**Acceptance:** Automated checks attempt to bypass the development policy through
direct session calls and payment intents, and inspect the packaged Release manifest
and binaries for test hooks. Development and release installs have separate data.
Mainnet access in a release candidate becomes available only through the documented
release process after the autonomous engineering gates pass; it is not silently
enabled by a development network selector.

### 2 Resolve the Android runtime compatibility issue

**Code:** Android and shared project files, dependency locks, focused compatibility
tests, and a retained minimal reproduction of the existing Mono failure.

- Reproduce the NBitcoin failure on .NET 10 Android Mono and isolate the exact failing
  API, runtime mode, and package version. Preserve the original diagnostic evidence.
- Test supported SDK/workload patches and released upstream compatibility fixes.
  Prefer the supported .NET 10 Mono path if it executes the existing engine correctly.
- Compare Debug and Release results for key derivation, serialization, signatures,
  transaction validation, and WabiSabi credentials. Use published vectors and Bitcoin
  Core as independent implementation checks where applicable.
- Select and pin a production-supported Android runtime route. Do not replace
  experimental CoreCLR with another experimental configuration, weaken validation,
  or write new Bitcoin cryptographic primitives to make a test pass.

**Acceptance:** The selected supported configuration passes BIP39/BIP32 derivation,
BIP84/BIP86 addresses, ECDSA/Schnorr and Taproot signing vectors, encrypted-key
round trips, WabiSabi proof checks, and the funded device suites in Release as well
as Debug. Record exactly which architectures were executed; packaging ARM64 is not
evidence that ARM64 executed successfully.

**Capability limit:** I can build the reproduction, investigate, integrate released
fixes, and implement ordinary compatibility changes. I cannot promise that an upstream
runtime defect is locally fixable. Microsoft still describes .NET 10 Android CoreCLR
as experimental. Its general support policy lists .NET 11 RC1 as a go-live release,
but that does not establish support for this particular Android workload and engine.
A future runtime route must be checked against its Android-specific support status.
If no supported route passes, this gate stays blocked while other work continues.
[Runtime guidance](https://learn.microsoft.com/en-us/dotnet/maui/deployment/runtimes-compilation?view=net-maui-10.0),
[support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

### 3 Protect credentials and define authorization boundaries

**Code:** New Android credential-storage and authentication components,
`WalletRuntime.cs`, `WalletSession.cs`, `MainActivity.cs`, and `MobileSettings.cs`.

- Move RPC passwords out of ordinary settings JSON into authenticated encryption
  backed by Android Keystore; migrate existing settings without logging the value.
- Review the existing encrypted Bitcoin-key format and password handling. Add a
  device protection layer where it improves the local threat model without breaking
  portable Wasabi backups or silently changing the recovery passphrase.
- Separate phone/app authentication from Wasabi's BIP39 passphrase. An app PIN or
  biometric change must never derive a different Bitcoin wallet.
- Require fresh authorization for send, backup disclosure, and sensitive settings.
  Biometrics can be optional convenience; retain a usable credential fallback.
- Lock the UI immediately on backgrounding. If an authorized CoinJoin needs keys,
  retain only its required signing authorization and deny unrelated wallet operations.
  Clear those keys when the manager finishes or stops, even if the app stays backgrounded.
- Clear transient secrets on screen exit, cancellation, and disposal; review signing
  key ownership and disposal. Minimize immutable secret strings and retained callbacks.
- Keep secrets out of intents, saved activity state, logs, notifications, screenshots,
  and clipboard. Bound and redact diagnostic logs.

**Acceptance:** Tests cover locked operations, backgrounding during an operation,
cancelled authentication, wrong passwords, idle expiry, signing-key cleanup after
CoinJoin, and Keystore invalidation. Restore with recovery words and the original
passphrase still works after device keys are lost. Searches of synthetic test logs
and stored settings find no injected secrets.

Keystore can protect a wrapping key; this does not make NBitcoin's signing keys
non-exportable hardware-wallet keys. Hardware security must be checked on the actual
device rather than inferred from the API name.
[Android Keystore](https://developer.android.com/privacy-and-security/keystore).

### 4 Make backup and recovery resilient

**Code:** `WalletSession.cs`, Android document import/export and recovery screens,
shared key-manager persistence where necessary, and restoration tests.

- Preserve the existing Wasabi key derivation and portable encrypted backup format.
  Specify supported BIP84/BIP86 accounts, gap scanning, and recovery limitations.
- Verify recovery words and the original passphrase against both supported account
  public keys before calling a backup verified.
- Make wallet creation, import, and sensitive file replacement atomic. Detect incomplete,
  truncated, invalid, or conflicting files without overwriting the last usable copy.
- Validate backup size, network, key structure, name, and signing capability. Handle
  watch-only imports explicitly, with signing unavailable.
- Handle document-picker cancellation, revoked access, low storage, and interrupted
  export. Confirm completed writes before reporting a successful export.
- Test restoration into a fresh app installation with no old database or device key,
  including recovery of funds beyond the first address gap and older imported wallets.

**Acceptance:** A fresh installation recovers the fixture's addresses, unspent outputs,
balance, and ability to sign from the offline backup. Corrupt and interrupted operations
preserve the original wallet. The UI accurately distinguishes portable recovery of
funds from labels or history metadata that a seed alone cannot restore.

### 5 Verify synchronization and network privacy under failure

**Code:** `WalletRuntime.cs`, `TorHost.cs`, `WalletSession.cs`, shared transport/filter
code if needed, and network fault fixtures.

- Audit every mobile network path: peers, compact filters, block downloads, transaction
  broadcast, fee estimates, exchange rates, coordinator requests, and personal RPC.
- Enforce Tor transport for every non-loopback wallet connection. Exercise Tor exit,
  SOCKS failure, DNS errors, TLS errors, redirects, and reconnects without a clearnet fallback.
- Give the app ownership of its Tor process, ports, control authentication, and restart
  state. Recover from a killed Tor child without stopping other Tor installations.
- Test an old-wallet rescan, interrupted filter download, peer churn, reorganization,
  conflicting spend, and network switching. Retain chain and transaction validation.
- Display stale/partial state accurately and prohibit new signing when synchronization
  or input availability is uncertain. Keep wallet data separated by network.

**Acceptance:** Node-backed fixtures confirm state after reorganization and reconnect.
Controlled captures and transport tests show no direct wallet traffic when Tor fails.
A recovered wallet never reuses an invalid scan position. Fee-data failure is handled
explicitly rather than silently presenting an arbitrary fee as a current estimate.
These checks establish the tested transport behavior, not a guarantee of anonymity.

### 6 Complete safe payment and fee management

**Code:** `WalletSession.cs`, a new pending-transaction store, payment/history screens,
`PaymentRequest.cs`, and shared `TransactionModifierWalletExtensions.cs` integration.

- Bind review to an immutable payment proposal: wallet, network, destination, amount,
  inputs, outputs, change, fee, and expiration. Preserve the exact reviewed transaction
  through local signing and broadcast.
- Make duplicate taps and concurrent wallet operations safe. Revalidate inputs,
  synchronization, authorization, dust, and fee policy immediately before signing.
- Persist a signed pending transaction before contacting the network. On timeout or
  process restart, reconcile or rebroadcast that same transaction rather than creating
  a second payment. Distinguish unknown broadcast outcome from definite rejection.
- Finish send-all, coin selection, fee estimation and manual override, spendable balance,
  and abnormal-fee confirmation. Keep amounts exact to the satoshi across UI locales.
- Expose existing engine fee speed-up through RBF/CPFP and cancellation where supported,
  with a new explicit review and authorization. Explain replacement limitations accurately.
- Show pending, confirmed, replaced, conflicted, and failed states with coherent history
  and transaction details. Preserve change-address handling and labels.

**Acceptance:** Bitcoin Core independently checks destination, amount, change, fee,
signatures, and acceptance. Fixtures cover broadcast interruption and restart, repeated
submission, send-all, stale review, mutated proposal, changed inputs, fee replacement,
cancellation races, and a conflicting transaction. No failure path silently creates
a second payment or changes the approved payment.

### 7 Finish camera and daily phone interactions

**Code:** `ScannerActivity.cs`, `MainActivity.cs`, native screen/state components,
Android resources, and a separate UI test driver.

- Replace the fixed camera orientation with sensor/display-aware handling, preferably
  through the native Camera2 API. Cover autofocus, preview sizes, decoding rotation,
  camera pause/resume, denied permission, and missing camera.
- Validate incoming BIP21 requests through camera, clipboard, and external intents using
  the same bounded parser. Requests may populate review; they must not authorize payment.
- Preserve ordinary send drafts through navigation without persisting secrets or
  allowing old callbacks to operate on a new wallet or a locked screen.
- Move lifecycle and operation state out of Activity callbacks where necessary so it
  can be tested consistently. Retain Wasabi's existing visual style.
- Verify small screens, landscape, keyboard insets, large fonts, TalkBack labels and
  focus, usable touch targets, long addresses, and clear network/sync state.
- Keep receive QR, displayed address, copied value, and shared request consistent.

**Acceptance:** Automated UI tests complete create, backup confirmation, receive,
scan/import request, review/send, fee management, history, recovery, and locking.
Virtual camera images verify the scanner pipeline. A physical camera test remains
required for your handset; a generated QR or byte-array decoding test cannot replace it.

### 8 Make Android background operation predictable

**Code:** `WalletService.cs`, `WalletRuntime.cs`, session lifecycle and shared worker
cleanup where needed, and Android interruption fixtures.

- Use explicit start, ready, reconnect, stopping, stopped, and failed states; serialize
  transitions and propagate cancellation and failures without unobserved async exceptions.
- Bound wake locks and work, release them on every shutdown path, and stop idle work.
- Handle foreground-service denial and timeouts promptly. Start work from supported
  user interactions and resume scanning after reopening rather than promising perpetual
  background execution.
- Before joining a CoinJoin, account for background restrictions and remaining service
  time where observable. Stop enrolling before an orderly shutdown; retain protocol
  protection for a round already in a critical phase.
- Test process death, force-stop, screen lock, Doze, battery saver, memory pressure,
  notification denial, Wi-Fi/mobile transitions, and service timeout during wallet work.
- Ensure abrupt termination cannot corrupt the wallet, cause address reuse, or lose
  the pending-payment record. Detect interrupted CoinJoins and reconcile chain state.

**Acceptance:** Forced interruptions leave a recoverable wallet, restart in a locked
state, reconcile transactions, and resume scanning without duplicate workers or Tor
children. API 35/36 timeout tests stop the service within Android's deadline. An OS
kill cannot be promised to finish a CoinJoin; tests must establish recovery and preserve
the underlying spending and protocol safeguards.
[Foreground service limits](https://developer.android.com/develop/background-work/services/fgs/timeout).

### 9 Resolve CoinJoin reliability and verify protocol safeguards

**Code:** `WalletSession.cs`, Android CoinJoin controls, `WalletInstrumentation.cs`,
`Contrib/Android/test-device.ps1`, the two regtest participants and coordinator,
and shared WabiSabi code only if the failure warrants it.

- Reproduce or otherwise establish the cause of the earlier output-registration exit
  using phase-specific diagnostics and controlled fault injection. Keep synthetic test
  logging separate from private user-wallet logs.
- Fix the underlying defect and retain a regression case. A later green rerun is not
  resolution of the unexplained earlier failure.
- Exercise registration, confirmation, output registration, signing, broadcast,
  coordinator restart, blame rounds, participant dropout, retries, exclusions, fee
  ceilings, safe stop, and key cleanup with independently keyed participants.
- Preserve solo-CoinJoin checks, minimum participation rules, input verification,
  coordinator checks, output verification, and signing protections.
- Make coordinator configuration, fees, failures, and stop state usable on a phone.
  Keep CoinJoin opt-in and available only after its separate release gates pass.
- Label privacy scores as estimates; do not describe regtest participation as proof
  of meaningful real-world anonymity.

**Acceptance:** The original failure has a documented resolution or a verified reason
it is outside the wallet, with a retained regression check. At least 20 consecutive
fresh two-participant rounds complete, plus deterministic cases for interruption,
blame/retry, rejected inputs, excessive fees, and stops in every protocol phase.
Every failed trial remains recorded; automatic retries cannot turn a failing trial
into a passing release check. The repetition count is a reliability gate, not a
statistical security or anonymity guarantee.

Ordinary receive/send does not require a coordinator. The full Wasabi feature claim
does require completing this workstream. I can run a real local coordinator and protocol
tests, but cannot create independent mainnet participants or guarantee an operator's
availability, liquidity, or privacy by myself.

### 10 Expand verification to the supported package and device matrix

**Code:** Mobile tests, Android instrumentation and UI driver, verification scripts,
`.github/workflows/android.yml`, and `VALIDATION.md`.

- Retain the existing engine checks and add focused regression and property/fuzz tests
  for security-sensitive parsing, persistence, concurrency, and failure handling.
- Pin SDK/workload/native inputs and CI actions. Run required checks on the same source
  tree used to package the candidate; retain failed runs and useful redacted diagnostics.
- Test the declared minimum Android version and API 35/36, ordinary 4 KB memory pages,
  and an actual 16 KB emulator configuration. Check its reported page size as well as
  native ELF and APK alignment.
- Execute ARM64 where an available supported emulator/runner permits it; otherwise
  leave ARM64 execution explicitly open for the phone check. Do not invent device evidence.
- Add black-box Release UI checks without compiling debug instrumentation or disabling
  the package's security flags. Verify both clean installation and upgrade behavior.
- Record CPU architecture, OS, page size, runtime, test outcome, source commit,
  unsigned package digest, distributed APK digest, and signing-certificate fingerprint.

**Acceptance:** The available automated matrix passes wallet, Tor, lifecycle, backup,
payment, and enabled CoinJoin checks on the candidate's code. Desktop locked restore,
build, and relevant tests still pass. Unsupported claims are removed from the support
list; unavailable ARM64 or handset execution remains a separate open device gate.
Release security flags stay enabled during package checks. Library alignment alone
cannot close the 16 KB execution gate.
[16 KB testing guidance](https://developer.android.com/guide/practices/page-sizes).

### 11 Review security and release dependencies

**Code:** Mobile/shared changes, dependency and native-library manifests, build workflow,
security review notes, and regression tests for discovered defects.

- Perform a source and threat-model review covering lost/locked phones, malicious
  requests/backups/coordinators, network interference, signing authorization, recovery,
  debug exposure, secrets, and the update channel.
- Run available analyzers and dependency vulnerability checks. Review native Tor and
  runtime provenance in addition to NuGet packages. Keep dependency/license notices
  and a software bill of materials tied to the release.
- Fix known seed-exposure, unauthorized-spending, corruption, and privacy failures;
  retain meaningful regression cases. Recheck nearby paths affected by each fix.
- Document remaining assumptions and findings. Prepare a compact reproduction/build
  package and review checklist that an independent reviewer can use.

**Acceptance:** No known unresolved critical/high finding affecting funds, recovery,
secrets, or the claimed network privacy remains. The unexplained runtime/CoinJoin
failures have not been relabeled as harmless without evidence. The review is explicitly
described as my own review; external review status remains separate.

### 12 Produce the signed APK and a safe update path

**Code:** Release packaging/verification scripts, Android version/package configuration,
CI configuration, release manifest, and installation/recovery instructions.

- Create and retain a dedicated production signing identity in protected local storage;
  keep the key/password out of source, logs, artifacts, and diagnostic output. Define
  how its owner retains a separate secure backup.
- Make production packaging require that identity. Do not pass a development certificate
  off as production, or replace an existing identity silently.
- Build the candidate from the verified commit with locked inputs. Check unsigned
  package reproducibility, identify any remaining nondeterminism, then sign and verify
  the distributed APK, native alignment, manifest, version, and certificate.
- Produce the APK, checksum, source/dependency manifest, concise installation guide,
  backup/recovery guide, and exact release verification record.
- Test an update signed with the same identity: wallet files, device protection,
  scan state, and pending transactions must survive. Reject unexpected signer/version.
- Detect an older development install with a different certificate. Use separate
  package data or a verified backup migration; never uninstall a funded wallet merely
  to overcome a signature mismatch.

**Acceptance:** A clean test installation and an update both pass with the stable
certificate, without exposing signing secrets or requiring deletion of wallet data.
The distributed package is the one represented by the recorded digest and source.
Instructions provide a repeatable sideload/update route; a Play Store account or
approval is not required to install a personal APK.
[Android signing and updates](https://developer.android.com/studio/publish/app-signing).

## Work requiring access or participation

These are not tasks I can honestly mark complete while working alone on this computer.
I can prepare the tests and perform the technical steps once their prerequisites exist.

| Requirement | What I can do | What remains outside autonomous control |
| --- | --- | --- |
| Your actual phone | Read its Android/CPU/page-size/security profile, install the verified APK, run UI and node-backed tests, check Tor, locking, restarts, and upgrades | You must connect/pair and authorize access, unlock when needed, and enable installation through the normal phone interface if required |
| Physical camera | Prepare known payment QR codes and verify decoded destination/amount and camera lifecycle | The phone must actually be pointed at an external QR; virtual camera testing is insufficient |
| Recovery backup | Implement and test confirmation, export, and fresh-install restoration using disposable fixture wallets | You must privately record and retain your own recovery words and original passphrase; they must not enter chat or test logs |
| Public Testnet4 funding | Run an external-network receive/send/recovery test if free test coins are available through an accessible supported route | Availability of an external faucet or other test-coin source cannot be guaranteed |
| Real mainnet payment | Prepare the exact transaction, verify the address/amount/fee, and reconcile broadcast and confirmation | You choose and authorize a small test amount and destination; I cannot spend your funds or obtain bitcoin on your behalf without explicit instructions |
| Independent security review | Prepare source, build evidence, threat model, and reproduce/fix findings | I cannot independently audit my own implementation or claim a third party reviewed it |
| Mainnet CoinJoin conditions | Implement the client and test the real protocol against a supplied compatible coordinator | Coordinator selection, service availability, independent participation, and real-world anonymity cannot be guaranteed by local tests |

Hardware-wallet support and public app-store publication are separate extensions.
They are not prerequisites for your stated use as a personal software wallet. The
desktop RPC server, Scheme console, and desktop updater also do not need Android
equivalents for that goal. None of these omissions excuses unfinished mobile spending,
recovery, privacy, or reliability work.

## Execution order and completion criteria

Start with workstream 1, then resolve the runtime in workstream 2. Implement credentials,
recovery, and synchronization before completing payments; build phone interaction and
lifecycle checks around those operations. Resolve CoinJoin with those foundations in
place. Finish the complete verification matrix and security review, then build and
validate the signed release in workstream 12. Integration tests grow with each change;
they are not postponed until packaging.

If the runtime gate is blocked upstream, I can continue the remaining source, tests,
and packaging work, but cannot claim a supported mainnet release. If only CoinJoin
remains blocked, a basic receive/send candidate must keep CoinJoin disabled and be
described accurately; that would not complete the original full Wasabi Android request.

The autonomous implementation is complete when the source work and available automated
checks in all twelve workstreams pass, the work is committed and pushed to remote
`master`, and the release record binds the APK to that verified source. Unavailable
architecture or handset checks remain marked pending; they are not counted as passed.
Readiness for your personal mainnet use additionally requires the supported runtime
and Release package to pass on your phone's actual architecture, a fresh-install recovery check,
and an explicitly authorized small receive/send/confirmation check on that phone.
My recommendation must report the remaining security-review limits; one successful
payment does not certify safety for substantial savings.

This produces a concrete installable candidate and a documented decision about use.
It does not promise that all upstream defects, Android vendor policies, or independent
security assurance are within my control. Calendar estimates remain uncertain until
the runtime failure is isolated; this is implementation work, not a final packaging step.
