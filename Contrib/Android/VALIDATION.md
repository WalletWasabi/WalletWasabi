# Android personal candidate verification

Recorded 2026-10-05 (Asia/Singapore). Version 0.3.0 / version code 5 is a
qualification candidate. **Release acceptance remains blocked by the uncompleted
checks in the delivered `verification.json`.** Source changes, earlier successful
tests and a signed APK do not qualify an untested handset or authorize bitcoin.
This implementing review is not an independent security audit.

## Runtime and native dependencies

The pinned baseline is .NET SDK 10.0.401, Android workload 36.1.69 and Mono
10.0.12 JIT. Trimming, interpreter, assembly store, ReadyToRun and AOT are disabled.
The original initialization deadlock and interpreter proof failure are retained
in `RuntimeProbe`. Existing Bitcoin algorithms and serialization are preserved.
Release AOT with trimming disabled was attempted separately and rejected with
XA1030; the retained `mono12-untrimmed-aot-build.log` records that failure.
Experimental Android CoreCLR is not selected.

Runtime checks cover genesis and serialization, published BIP39/32/84/86/340
vectors, ECDSA verification and rejection, marshalling and managed WabiSabi
proofs. Funded regtest checks require Bitcoin Core acceptance of Segwit and
Taproot signatures.

`NativeSources/source.lock.json` pins the runtime, Android SDK, Tor 0.4.9.13 and
vendors, SQLite 3.53.3, NDK r29 and CMake 3.31.8. All 38 staged native files pass
16 KB LOAD/RELRO alignment, immediate binding and nonexecutable-stack checks.
No SDK pack is overwritten, ELF security header patched or protection waived.
Personal builds require verified source libraries. Inspection also checks APK
bytes against the native manifest and SDK assembly-container layout.

The API 24 TLS deadlock was reproduced as nested `SSLStreamHandshake` entry
during certificate rejection while legacy Conscrypt held its mutex. The scoped
source guard rejects reentry. Separate API 24/25 missing-anchor handling supplies
the verified public ISRG Root X1 certificate only when that anchor is absent;
full chain, signature, validity, usage and hostname checks still apply. Seven
host trust regressions pass. Public Tor TLS, fail-closed shutdown and restart
pass on API 24; the packaged probe passes all 15 valid/untrusted/wrong-hostname
connections. Repeated Activity creation is tested without starting two probes.
Earlier failing native/TLS diagnostics remain retained.

The first rebuilt loader failed with decompression error -31 because its local
container stub had 10 sections whereas the pinned SDK container has 11. Constants
now come from the hashed SDK stub; incompatible metadata is rejected. Corrected
source loader execution passes x64 tests. Corrected ARM64 handset execution is
still required; the earlier stock-runtime handset result is separate evidence.

## Verification records and architectures

`test-device.ps1` records source commit, dirty-tree state, APK checksum, package,
configuration, actual API/page size/architecture/runtime and result/log hashes.
Incomplete or failed runs never become PASS. CI uploads synthetic logs and
verification records, excluding wallet fixtures, databases and vaults. Delivery
binds selected evidence to clean published source and the inspected APK. Earlier
checks qualify only their recorded source/build.

Funded emulator execution is **x86_64**, even where ABI lists advertise ARM
translation. The SM-S948B separately passed the isolated stock Mono 10.0.12
Release crypto probe on **Arm64**, Android 16/API 36, 4096-byte pages
(`handset-runtime-probe.log` and its verification JSON). It has not passed the
complete source-native wallet, hardware authorization or camera qualification.

At source `0321b93ea78aca6a1c77cf27165eb48973397425`, Android CI run `37262644418`
verified the locked native build, full API 35/4096-byte and API 36/**16384-byte**
Debug/Release suites, and 20 consecutive fresh Release CoinJoins. All 20 unique
transactions completed on Android and an independently keyed host, then were
accepted and mined by Core. Its API 36/4096-byte TLS fixture failed when repeated
Activity creation restarted the probe; the corrected fixture passes locally.
API 24 CI boot was cancelled during an unbounded boot-property query; that job
executed no runtime tests. Desktop CI run `37262644316` passed Windows, Linux,
macOS Intel/ARM and Nix checks at that source.

The CI emulator is now pinned to the locally tested build, API 24 uses the same
Google APIs image, and boot-property probes alone have a two-second bound.
Instrumentation keeps its own deadline. These changes need a new remote run;
configuration alone is not a passing result.

Source-native local Release runtime/wallet/fault/vault suites pass on API 24
(`device-b6adafd5f7924356af0fbeef87b6f8a1`) and API 36/16384 bytes
(`device-18261fafb1f34476934c7b1dc6da7430`). API 24 Tor passes in
`device-af7b42ef9641424785f401a5bb85660e`; packaged TLS passes in
`device-tls-9ec0b59fbf3444c7ac3fd6e81ee27759`. Consult delivery JSON for the newest
matrix, exact checksums and latest source qualification.

## Payments, recovery and authorization

Funded fixtures cover both account derivations, persisted receive/change
addresses, exact recipient amounts, wrong password, expired review, cancelled
authorization, duplicate confirmation, RBF, cancellation, CPFP, interrupted
broadcast, same-height reorganization and confirmation. Signed bytes are
journaled before broadcast; ambiguous submission reserves inputs and reconciles
or rebroadcasts those same bytes. Fresh-directory encrypted backup recovery
requires neither the old database nor device keys and restores signing.

The self-transfer fixture reproduced desktop RBF deducting fees from an approved
owned recipient. Mobile proposals now mark owned recipients and protect their
exact script/amount during RBF; absent legacy markers protect all owned outputs.
Eligible CPFP remains the fallback. Core accepted and mined the protected
replacement (`device-0154a8a40f074d44b1bd303f5103a0a0`). Two journal regressions
preserve legacy JSON serialization and owned-recipient markers across
reconciliation. All 50 current mobile tests pass.

RPC vault tests cover encrypted persistence, tamper, removal and key loss.
Hardware-backed per-use biometric/device-credential success and cancellation
require the phone. Device credentials do not alter Bitcoin derivation or replace
the original recovery password.

A co-signed pure-Java harness drives the actual personal APK with release
security flags intact: wrong-password rejection, duplicate taps, background and
two-minute inactivity locking, delayed unlock rejection, process death, journal
preservation and updates. Preliminary version 0.3.0 package checksum
`96658ff503a6788274746f14a3ab0c405d6a5eaaafaa43b73d2a53f4f44b82f3` passed this
suite on API 36/4096 bytes (`native-ui-902f35a54eff4e968ae71a526e951933`). This is
**not the final delivery checksum**. Final bits and the version-4-to-5 update
require separate recorded checks.

## CoinJoin failures and public networking

Denomination construction's deferred random script selection was reproduced
and fixed by materializing choices once. Its regression failed before the fix.
An older historical output-registration timeout had insufficient private traces:
the correction does not uniquely establish that historical timeout's cause.

Further regressions reproduced uncancellable waits for credential dependencies
and an initial issuance failure leaving graph workers blocked. Waits now observe
cancellation; every issuance participates in sibling failure cancellation. A
vanished round propagates `RoundNotFound` promptly. Both old behaviors fail the
retained tests; all 325 WabiSabi host tests now pass.

Android fixtures exercise stop at input, confirmation, output, pre-signing and
post-signing phases. All five local source-native cases passed. Critical phases
complete safely, Core accepts/mines completed rounds, and stop clears signing
credentials while the interface is locked. Signed checkpoints survive every
coordinator failure until transaction or confirmed-conflict reconciliation;
six regressions retain those reservations.

The deterministic three-participant blame fixture withholds a signature, kills
that fixture process, completes a two-participant blame round and verifies its
excluded input remains unspent (`device-e36182ca0f4c406abb39ed130ceff6a1`). The
separate confirmation fixture withholds the actual coordinator confirmation
action; events emitted after confirmation are insufficient evidence. Restart
uses the same coordinator directory and requires a different successful round.
It passed in `device-c3c9ab77ff0a43c09cef4bde13844956`. Random registration windows
require a longer fixture deadline without reducing privacy delays or minimum
participation. Earlier deadline failures remain recorded.

The process-death fixture force-stops Android after a durable signing checkpoint,
reopens without clearing data, verifies uncertain reservations and absent signing
credentials, and requires confirmed-conflict reconciliation before release.
Use delivery JSON for the final nine-scenario results and their hashes.

Public fee and unfunded Main/Testnet4 synchronization fixtures exercise actual
Tor transports without RPC credentials or real-bitcoin submissions. They are
separate from regtest CPFP tests. Earlier public fee requests timed out through
Tor and remain failures until a recorded production-provider pass. Twenty local
rounds or deterministic fault tests do not establish real-world anonymity.

Fee-cache regressions also reproduced duplicate concurrent insertion and periodic
updates returning old cached information without a network fetch. Both are fixed;
all 39 CPFP host checks pass. Four HTTP retry tests verify discarded responses
are disposed before the next attempt; three existing retry-policy tests pass.
These host checks do not substitute for public service execution through Tor.

## Required external gates

- Corrected source-native ARM64 execution and full wallet tests on the connected
  phone, including hardware authentication, physical camera, accessibility,
  network/background/battery behavior, recovery and updates.
- A privately retained backup and a new disposable mainnet wallet's small
  receive/send/confirmation test with the user's explicit amount, destination
  and fee budget. Existing funded seeds are never test fixtures.
- Production coordinator availability; an independent review, if sought, remains
  external and is not implied by this work.

Hardware wallets and Play Store publication are outside this release. Preserve
the signing key and wallet data. The delivery record is authoritative for final
package/source identity, executed checks, failures and remaining gates.
