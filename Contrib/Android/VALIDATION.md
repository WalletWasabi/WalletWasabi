# Android personal candidate verification

Recorded 2026-10-06 (Asia/Singapore). Version 0.3.11 / version code 16 is a
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
source loader execution passes x64 tests. Corrected ARM64 handset execution
passed at clean source `76ab3fa9`; the earlier stock-runtime handset result is
separate evidence.

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

At source `3ccfd8b45895fab71a69891b7708cd65e46a9d2b`, Android run `37277210233`
passed its native build, API 35 and both API 36 Debug/Release suites, and all
20 consecutive fresh Release CoinJoins. The retained 21-mode verification record
is in `ci-3ccfd8b4-consecutive/device-6aa02bc98c0a4fa7a259597e72a5a0dc`.
API 24 again failed before emulator boot, executing no wallet tests; a 2 GB
configuration and kernel diagnostics are being checked separately. Its signing
interruption fixture attempted two process-global engines simultaneously;
the corrected fixture disposes one before recovery and passes locally.

Desktop run `37277210338` exposed a Windows configuration sharing race and a
Nix blame-test failure. Configuration saves now share a path lock and publish
complete files atomically; initial creation is synchronous and read errors do
not replace valid settings. Six settings and two safe-file tests pass. The blame
fixture used registration windows below Arena's one-minute threshold, phase
windows below client safety margins, an unintended one-coin isolation policy,
and a test factory that discarded the blame minimum-input argument. These test
conditions are corrected without changing production privacy or participation
rules. The exception-to-success shortcut was removed; three consecutive actual
blame rounds and the full 325-test WabiSabi suite pass locally. New remote results
are still required.

At clean source `24abf94cb4ade34018d9f4548f1ca3ad2997f41e`, Android run
[`37285067882`](https://github.com/nopara73/WalletWasabi/actions/runs/37285067882)
passed every job: the locked native build with OpenSSL 3.5.9; API 24, 35,
36/4096 and 36/**16384** Debug/Release suites; all nine deterministic CoinJoin
failure scenarios; and 20 consecutive fresh Release rounds. Downloaded records
in `ci-24abf94c` contain 18 PASS verification files and 78 executed mode logs.
Every recorded log hash was checked, with clean source, actual x64 Mono 10.0.12
execution and requested page sizes. The consecutive-round qualification APK is
`02e485fbde23fe4ba7040c506f6ad4f6643fe0cf0aafae3b0c41197673ab50c4`.
These results qualify that source, not subsequent changes or the phone.

Desktop run [`37285067800`](https://github.com/nopara73/WalletWasabi/actions/runs/37285067800)
passed Linux, Nix and both macOS architectures. Windows passed 1102 of 1103 tests;
the retained blame fixture failed. Affinity-limited synthetic reproductions
established that three credential-producing clients and their coordinator exceed
the fixture's shortened 30/60-second phases on one contended CPU. Retained request
timings separate local cryptographic work from subsecond HTTP requests; the
coordinator rejects late work with `WrongPhase`. The test now uses the production
three-minute fail-fast output budget, retains real blame/input assertions, and
records only its own synthetic round diagnostics. Qualification of this correction
is recorded separately; no production CoinJoin protection was relaxed.

The corrected fixture passed with its coordinator and all three clients confined
to one CPU (`blame-production-budget-compiled-one-cpu-tests.log`, 6m13s).
Both honest participants completed the blame round, its four expected inputs were
checked, and the withheld-signature participant was excluded. The earlier
30/60-second failures and request timings remain retained.

Source-native local Release runtime/wallet/fault/vault suites pass on API 24
(`device-b6adafd5f7924356af0fbeef87b6f8a1`) and API 36/16384 bytes
(`device-18261fafb1f34476934c7b1dc6da7430`). API 24 Tor passes in
`device-af7b42ef9641424785f401a5bb85660e`; packaged TLS passes in
`device-tls-9ec0b59fbf3444c7ac3fd6e81ee27759`. Consult delivery JSON for the newest
matrix, exact checksums and latest source qualification.

## Payments, recovery and authorization

The subsequent `41f878af` run (`37297289462`) passed API 24 and 35, the
source native build, and 20 consecutive Release CoinJoins. Its nine deterministic
CoinJoin scenarios also passed, but their artifact upload failed at GitHub; the
completed job log is retained. API 36/4 KB rejected a CPFP child, and API 36/16 KB
rejected a cancellation. Both remain failed evidence. The old broadcaster lost
Core's specific rejection behind its generic error-code description. It now
retains the node message, with a regression, and synthetic-only instrumentation
records final size/fee/locktime and Core's policy result for uncertain receipts.
Current-source matrix qualification must be recorded separately.

Two cancellation regressions reproduce insufficient replacement fees because
the original unsigned-size check omitted witnesses. Five input/fee cases pass
after estimating the signed size. A funded Core 31.1 fixture with 1 sat/vB
incremental relay policy rejects the old 96-satoshi bump where 110 is required;
all five corrected cases are accepted. Its default policy accepts the old cases,
so this defect alone is not asserted to explain the earlier CI rejections.

Three deterministic locktime cases reproduce selecting the next block or
unsigned underflow at low heights. Core's default policy rejects a signed
transaction at height 102 with locktime 103 as `non-final`, then accepts the
corrected locktime 102. Future samples now use the current tip, and backward
samples clamp at zero. Confirmation also rechecks reviewed height locktimes
after reorganization. Bitcoin signing and serialization are unchanged.

The diagnostic Release engine at modified `41f878af` passed funded wallet and
fault/reorganization/CPFP checks on API 35 in
`device-3bfcfc32fb224fe180216d54ccb6d500`. This predates the locktime correction
and does not qualify the final clean source. CI artifact patterns now select
top-level text logs and Core's named debug log, excluding LevelDB log files.

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

Actual coordinator confirmation withholding passes in
`device-ce573f1f71264385b28a1a7cfb280c2f`: both honest participants finish and
the excluded input remains unspent. Signing process death passes in
`device-e588d357cc41484bbcba1fb0a0b9884d`: a fresh-directory encrypted recovery
signs and mines a conflicting spend, then the reopened original observes that
confirmed conflict before releasing its reservations. Neither uses previous
device keys or a funded user seed.

The process-death fixture force-stops Android after a durable signing checkpoint,
reopens without clearing data, verifies uncertain reservations and absent signing
credentials, and requires confirmed-conflict reconciliation before release.
Use delivery JSON for the final nine-scenario results and their hashes.

Public fee and unfunded Main/Testnet4 synchronization fixtures exercise actual
Tor transports without RPC credentials or real-bitcoin submissions. They are
separate from regtest CPFP tests. At source `24abf94c`, live production CPFP
provider execution passed for Main and Testnet4 in
`device-ed11374569524885a226a04140a88999`: Tor bootstrap, raw transaction hash,
fees, virtual sizes and ancestor units were checked against the official onion
service. Earlier HTTPS-through-Tor timeouts remain recorded. Twenty local rounds
or deterministic fault tests do not establish real-world anonymity.

Fresh public synchronization remains a distinct gate. One run reached 574,000
mainnet headers but exhausted its 12-minute fixture budget. Its Testnet4 peers
advertised no compact-filter service, so the engine correctly refused to use
them for filters. A 46-minute host sleep is recorded separately in
`host-suspend-evidence.json`; failed instrumentation remains failed. The fixture
now allows 40 minutes per network and reports actual header height. Peer discovery
also queries the existing DNS seeds with Bitcoin's `x49` service mask through
the configured resolver, retaining general answers and handshake validation.
Three regressions fail before that correction and pass after it. The mask follows
[Bitcoin Core's seed queries](https://github.com/bitcoin/bitcoin/blob/master/src/net.cpp)
and [bitcoin-seeder's service whitelist](https://github.com/sipa/bitcoin-seeder/blob/master/main.cpp).
This change requires a new real Tor synchronization result.

Fee-cache regressions also reproduced duplicate concurrent insertion and periodic
updates returning old cached information without a network fetch. Both are fixed;
all 39 CPFP host checks pass. Four HTTP retry tests verify discarded responses
are disposed before the next attempt; three existing retry-policy tests pass.
These host checks do not substitute for public service execution through Tor.

Additional transport diagnostics reproduced mempool.space connection timeouts
before TLS in both the Android client and host curl using the same Android Tor
process; the official onion endpoint responds. CPFP now shares the onion selection
already used by mining-fee estimates. Two Main/Testnet4 route regressions fail
against the prior implementation, while HTTPS remains selected for direct desktop
operation. Two P2P regressions also reproduce publishing a cached filter tip as
current before network catch-up. The provider now waits for the reported tip and
headers; signing and reservation reconciliation require matching network/filter
tips. All 17 routing/filter/synchronization checks pass after correction. Public
execution and the final APK are qualified separately; earlier timeout failures
are retained.

## Credential dependency cycle correction

Desktop CI `37403699733` retained a Mac Intel blame failure: the third honest
participant completed seven credential requests but never registered outputs.
Its exact graph values were not retained, so that old trace alone does not identify
its unique decomposition. A deterministic production-denomination reproduction
with seed 23 produces a zero-amount edge from node 12 to ancestor 18, while
18 -> 16 -> 12 already carries positive amounts. Seven requests can complete;
the remaining cycle cannot execute. The pre-correction graph test fails with
this retained graph, independently of CPU timing or coordinator availability.

Zero-credential routing now checks reachability across both credential types before
adding an edge. No-progress routing fails explicitly; the scheduler rejects
incomplete/cyclic graphs before requests. Amounts, fees, outputs and protocol
degree limits remain unchanged. A literal-value regression retains the failing
graph; 500 seeded production decompositions and 2,000 generated graphs are also
checked. The cryptographic blame fixture uses seed 23 for the third independently
keyed participant. All 1,131 desktop unit tests passed locally in 8m16s, including
its four-honest-input blame transaction and 79 dependency-graph cases. The retained
phase trace shows both honest clients signed the same final transaction. Final
Android/current-source CI execution is recorded separately in delivery evidence;
earlier CI passes are historical.

Credential allocation has a separate deterministic regression: reordered required
values and missing duplicates were silently dropped by a sequential scan/Zip.
Required credentials now match values with multiplicity, are allocated once and
follow graph order; missing credentials fail before any output promise is completed.
Seventeen focused cases pass after this correction. Normal graph sorting means this
allocator flaw alone cannot explain the retained Mac trace.

Fresh Testnet4 P2P/filter/wallet synchronization through Tor reached height 155324
on API 35/Release at clean `3299d9b2`, in `device-3a22dc18bc5c4f949830e36021fb5ae0`.
That aggregate run remains FAIL because its preceding cold mainnet Tor bootstrap
timed out. Independent public-network selection retains this distinction; Info
logging avoids exporting generated receive addresses from these unfunded fixtures.

## Public P2P and cold Tor startup correction

At clean `5d51713b`, local Release public Testnet4 synchronization stopped at wallet
height 69500 while block/filter headers reached 155328/155327. The next range at
69501 timed out repeatedly. The retained complete Info log and native thread dump
do not uniquely establish every thread's managed call stack. The unfunded fixture
was intentionally stopped after retaining those diagnostics; its record remains
FAIL. A controlled local NBitcoin transport reproduces a concrete defect: a
stalled synchronous compact-filter send holds the assignment lock and prevents
the shared ticker from reaching timeout cleanup. Both pre-correction header and
filter regressions fail. Sends now use bounded asynchronous waits, observe late
failures, cancel on detach and release ranges for reassignment. NBitcoin's send
filters also report exceptions without completing the returned send task; two
additional negative cases require that callback to disconnect and release ranges.
All 37 focused networking checks, 60 mobile checks and 1,143 desktop unit tests
pass after correction. The full local unit run completed in 7m07s with no failures
or skips. Its earlier broad run included acceptance/external API cases while
bundled binaries were omitted; that failed invocation is retained separately.

Separate deterministic P2P tests reproduce partial filter batches replacing a
higher announced network tip and a caught-up response erasing a newer announcement.
P2P consumption now advances the reported target atomically. Authoritative RPC
responses retain their ability to lower the target after a reorganization.

Android CI `37407957108` at `5d51713b` passed native builds, API 24/35/36 with 4 KB
pages, nine failure scenarios and twenty consecutive rounds. Its API 36/16 KB
Release Tor test failed: bootstrap reached 73 percent but the fixed three-minute
startup deadline expired before completion. A separate cold public mainnet fixture
also timed out at 50 percent. These failures remain recorded. Startup now allows
progress beyond three minutes while bounding idle time to three minutes and total
time to ten; monotonic-clock tests cover progress, repeated notices, stalls and
the absolute cap. The Tor fixture's outer deadline reflects those bounded waits.
New-source actual public synchronization and matrix execution remain required.

The selected desktop coordinator `https://coinjoin.kruw.io/` returned HTTP 301 for
the engine's `/wabisabi/status` request, redirecting to `https://kruw.io` rather than
returning WabiSabi JSON. This read-only request used the bundled Android Tor SOCKS
transport and normal HTTPS certificate validation, with no input registration.
The original two-field coordinator bootstrap is retained pending a working URL
and identifier from the user. Synthetic coordinator tests do not qualify this
external service's availability.

## Qualified version-8 source and live-fixture limits

At clean source `557eccb86e2f3ce9a3dfacd307af9f95bd902f98`, Android CI
`37415026438` completed successfully. The downloaded records independently verify
all 38 native source payloads, eight Debug/Release API/page configurations,
nine disruption scenarios and twenty fresh independently keyed CoinJoins.
All 18 records are clean; all 78 executed mode logs match their hashes. Twenty
distinct CoinJoin transaction IDs are retained; the fixtures require Core mempool
acceptance, mining and confirmed-wallet reconciliation. Desktop CI `37415026410`
passed all five jobs, 1,143 unit tests per platform and 39 Nix integration checks.
Its complete log is retained and hashed.

The personally signed version-8 APK at that source passed fresh-install payment,
password rejection, duplicate submission, background/inactivity lock, late unlock,
process restart, exact-byte reconciliation and an actual version 7 to 8 update.
Both UI fixture records and all eight mode logs are independently hash-verified.
Source-native Release crypto, SQLite and repeated TLS checks also pass on API 35
x64. The delivered package's own manifest remains authoritative for its identity.

The first version-8 public-fee aggregate remains FAIL: a live mempool sample's
raw transaction endpoint returned HTTP 404 after a preceding CPFP timeout. The
fixture now skips only unavailable raw-transaction samples and still requires a
valid production CPFP response on each network. A subsequent fixture's six-minute
outer budget interrupted a progressing cold Tor bootstrap; it remains FAIL.
The bounded twenty-minute fee budget preserves the Tor startup deadline and
allows subsequent reads. The corrected fixture passed Main and Testnet4 production
CPFP parsing through Tor. These fixture edits change no production wallet code.

The fresh mainnet synchronization fixture advanced to header 338000 before its
forty-minute limit; it did not qualify synchronization. Its subsequent cold
Testnet4 startup, and another standalone cold startup, hit Tor's idle deadline.
Those records remain failed. Full mainnet/Testnet4 header validation now has
separate 180/90-minute absolute fixture bounds and a ten-minute chain-progress
limit. An explicitly selected synthetic Tor cache can test retry behavior while
wallet, database and P2P header state remain fresh. Cached Tor startup is recorded
separately from a cold-start result and cannot erase its failure. Actual public
synchronization remains an open gate until the new records pass.

Later fixture builds and commits have their own checksums and source records.
The earlier clean CI results are not relabeled as execution of a later build.

## Header progress and interface responsiveness

The clean version-9 Release fixture at `f320c3a1` reached Testnet4 height 155339
through Tor, with wallet, filter and block-header tips synchronized. It explicitly
reused the retained synthetic Tor cache after a cold-start idle timeout, while
wallet, database and header-chain state were fresh. The cold failure remains
recorded; the warm retry does not establish cold-start reliability.

A controlled NBitcoin peer reproduces another transport defect: a peer can
complete the handshake, advertise a higher height and never answer `getheaders`,
without the existing chain behavior retiring it. Header downloads now have a
two-minute monotonic validated-progress deadline. Empty responses cannot extend
it, caught-up peers remain available, and invalid headers are retired immediately.
NBitcoin still validates proof of work and cumulative chain work. All six actual
wire-transport checks pass; one timeout case failed before the correction.
The full local 1,149-unit suite and all 60 mobile checks pass. Android execution
and CI of this new production change require their own final-source records.

Password verification for biometric unlocking/enrollment and replacement
preparation/signing now executes off the interface thread. Ordinary unlocking
also captures and clears the password field before scheduling work. A background
or inactivity lock, changed session, or cancelled request invalidates completion;
per-use device authorization retains its original checks. These paths require
actual signed-package UI and handset qualification.

The first version-8-to-9 UI attempt encountered Android's "System UI isn't
responding" dialog on the baseline version-8 build before installing version 9.
The failure and fixture data are retained. Restarting the owned emulator with the
CI's 4 GB memory allocation is a retry condition, not proof of the unique cause.
Neither this interface correction nor the peer timeout is asserted to explain
every earlier public-network or System UI stall.

## Peer-estimate expiry

Two additional actual-wire regressions reproduce a departed peer's advertised
height remaining as a permanent synchronization target. A validated height-one
chain remained blocked by an expired height-two estimate, and removing all peers
left their estimate behind without distinguishing cached filters from network
knowledge. Both cases fail against the old behavior and pass after correction.

Filter-header bookkeeping now separates validated progress from per-peer
estimates. Disconnecting or detaching removes only that peer's estimate; live
higher estimates still prevent premature readiness. Partial responses cannot
erase active estimates or validated progress. Authoritative RPC height updates
replace estimates and can still lower the target after reorganization. A final
actual-transport case covers subsequent departure after an RPC reorganization.
All nine actual-transport cases, the full 1,152-unit suite and all 60 mobile
checks pass after correction, with no failures or skips. The full local unit
run completed in 8m46s. Release compilation also passes without warnings or
errors. Android/public network and final signed-package records must qualify
this correction separately.

The earlier unfunded mainnet fixture reached validated header height 970145,
but its reported peer target rose above 975800 and it hit the ten-minute progress
limit. A read-only Blockstream tip request through the owned Android Tor returned
970145; a separate mempool request timed out. This corroborates an overstated
estimate but does not identify the original advertising peer or establish every
cause of that fixture's earlier slowdown. Failed records are retained.

At clean version-10 source `54543039`, the actual personally signed APK passed
fresh-install payment, password rejection, duplicate submission, background and
inactivity lock, late unlock, restart and exact-byte reconciliation. An actual
version 8 to 10 update preserved wallet access, encrypted RPC credentials and
pending-transaction bytes, and its regtest payment confirmed. Those checks
qualify version 10 and do not replace later-version execution.

At clean source `76ab3fa9`, version 11 also passed a fresh actual-APK funded
regtest payment, wrong authorization, duplicate taps, background/inactivity lock,
late unlock, process death, exact pending-byte reconciliation, same-version
reinstallation and confirmation (`native-ui-ca48af1eb0ee484686c2e44b9ded7486`).
A fresh unfunded Mainnet wallet synchronized through Tor in the separately
packaged Release engine (`device-public-main-7b4d6aea036341faa9cd97ee42fb873c`).
Its wallet/database/header chain were fresh; only the named Tor cache was reused.
This successful warm-cache run does not establish cold-start reliability.
The interrupted version-10 run remains INCOMPLETE and its recovered logs remain
retained. No actual bitcoin was submitted by these public synchronization checks.

The corrected source-native probe at that same clean source passed the published
vectors, SQLite 3.53.3 and all 15 TLS trust/hostname connections on the physical
ARM64/API 36/4096-byte phone. Duplicate activity creation started one probe only;
the temporary localhost private key was removed and system trust was unchanged.
Version 11 was installed with the personal certificate and the user reported
reaching the new disposable wallet home screen. These records do not establish
biometric, physical-camera, recovery, update or real-payment qualification.

Version 12 uses the original desktop logo and requested "Unfairly private" line,
allows screenshots throughout the app, and removes personal-node settings and
credential access from normal mobile startup. Legacy node credentials are retired
without decrypting them. Synthetic regtest node injection is separate, restricted
to localhost/regtest, and rejected by Android on physical devices. The actual-APK
UI harness checks both historical blocked and current enabled screenshot policies.
Synchronization now shows real stage counts, a progress bar and elapsed time;
remaining time uses monotonic observations for that stage and expires on stalls,
target changes or backward progress. Six deterministic progress checks and the
public-network node-injection prohibition pass in the 65-test mobile suite.

Version 13 makes device unlocking automatic after verified password access or
wallet creation/recovery on supported hardware. Wallet selection and confirmation
use the system prompt by default, with original-password fallback; the settings
enrollment controls are removed. Cancelled prompts never invoke signing actions,
and cancelled enrollment cannot override a background lock. Delayed password
verification now explicitly relocks the engine when its UI generation expires.
The actual-APK harness exercises a synthetic unavailable device key without
replacing an existing vault record, and verifies original-password recovery.
This tooling is separate from successful physical Keystore authentication,
which remains a handset gate until it has actually executed.

Version 14 uses "Wasabi Wallet" as the main screen title at the user's request.

Version 15 binds queued idle-service stops to the foreground generation and
runtime identity. A resumed or replaced runtime revokes the queued decision,
and its idle condition is rechecked on the main thread. Foreground refresh can
restart a fully stopped runtime. The older version-11 update fixture's lost
session remains an unexplained failed run. A separate actual-APK timing exercise
delays background decisions before resuming. Earlier version-14 runs passed;
a later run failed while the runtime log recorded process exit. Both outcomes
are retained, and they do not uniquely explain the older failure. Its earlier key-loss check
failed because the harness omitted the network directory in the wallet path;
the harness now uses the verified RegTest path. State-transition checks cover returning
to the app, a second background period and replacement-runtime identity. All
69 mobile checks pass after the change; hardware and actual-APK execution remain
separate gates until their records report success.

Version 16 shows a small busy indicator during operations. The actual-APK
late-unlock harness now waits for the password screen while continually checking
that delayed work cannot reopen wallet details. The prior version-14 update run
failed when it tapped during still-active password verification; its log is retained.
The synthetic independently keyed CoinJoin participant now injects its localhost
regtest node through `RegtestNodeOptions`. The version-15 CI run caught its stale
references to removed mobile RPC settings; those compile-error logs are retained.
All five version-15 desktop CI jobs passed with complete source-bound logs
(1,152 unit tests per job and 39 Nix integration tests). Android and CoinJoin
qualification for the final source remains required.

## Required external gates

- Full wallet tests on the connected phone, including hardware authentication, physical camera, accessibility,
  network/background/battery behavior, recovery and updates.
- A privately retained backup and a new disposable mainnet wallet's small
  receive/send/confirmation test with the user's explicit amount, destination
  and fee budget. Existing funded seeds are never test fixtures.
- Production coordinator availability; an independent review, if sought, remains
  external and is not implied by this work.

Hardware wallets and Play Store publication are outside this release. Preserve
the signing key and wallet data. The delivery record is authoritative for final
package/source identity, executed checks, failures and remaining gates.
