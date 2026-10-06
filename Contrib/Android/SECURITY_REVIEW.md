# Android source security review

This is a source review by the implementing agent, recorded 2026-10-06. It is
**not independent, not a penetration test, and not an audit or guarantee**.
Release acceptance is blocked by the findings and missing device evidence below.

## Reviewed boundaries and corrections

| Boundary | Correction / verification |
| --- | --- |
| Mainnet in development | Per-channel session assembly rejects mainnet even if a caller supplies the personal policy. Package/data identities are separate. |
| Secret persistence | Personal-node settings and credential access are removed. Android Keystore AEAD binds encrypted wallet passwords to package/reference; per-use retrieval requires hardware-backed authentication, otherwise password-only. Existing cipher tamper/key-loss tests cover the shared encrypted envelope implementation. |
| Authorization and lock | Device unlocking enrolls automatically after verified password access or wallet setup on supported hardware. Wallet selection and operation confirmation use the system prompt by default, with original-password fallback. Settings have no opt-in control. Fresh payment/replacement/backup/CoinJoin authorization; secrets released after non-CoinJoin use; stop releases retained CoinJoin secrets while locked. Cancelled/queued biometric callbacks retain Java peer lifetime instead of being disposed early. UI lock generations invalidate delayed completions, and a delayed password unlock explicitly relocks the engine after backgrounding. Busy operations and recovery-word screens no longer bypass inactivity locking. Hardware authorization still needs the phone. |
| Review versus signing | Immutable public details, private PSBT snapshot, wallet/network binding, monotonic five-minute expiry and serialized confirmation. The transaction factory's unsigned Signed flag was corrected and covered by a regression. |
| Ambiguous broadcast | Exact signed bytes are durably journaled before broadcast, inputs stay reserved, duplicate confirmation returns the same receipt, reconciliation never constructs a new payment. Failed journal writes conservatively retain in-memory reservations and never broadcast. |
| Replacement | Mobile RBF preserves foreign and explicitly marked owned recipient scripts/amounts. Legacy journals without markers conservatively protect all owned outputs. Eligible CPFP is the fallback; unavailable cases fail. Funded self-transfer reproduces the old fee deduction and proves the new protected replacement through Core acceptance/mining. Desktop defaults remain unchanged. Public CPFP service qualification is separate. |
| Immediate relay | Cancellation's incremental fee now uses signed size when preparing without credentials. Two failing fee regressions and funded Core rejection/acceptance records are retained. The shared locktime selector no longer chooses the next block or underflows at low heights; Core reproduces the old `non-final` rejection. Mobile confirmation rechecks height locktime after reorganization. |
| Reorganization / synchronization | RPC filter hashes come from the RPC chain; batch parsing is awaited, transport failure retries without rollback, and a same-height replacement is detected. Cached filters require a reported matching network tip; P2P cannot publish a cached tip as current while headers are behind the reported network. Signing and reservation reconciliation use the same readiness check. Two new cases fail before correction; all 17 routing/filter/synchronization tests pass. Funded Android reorganization/reconfirmation has separate retained evidence. |
| Recovery / address allocation | Both account public keys checked, import scan height reset, atomic complete-file publication with flush, directory flush on Android/Linux, and address persistence before disclosure. Fresh-directory encrypted recovery and restored signing passed. |
| CoinJoin | Denomination randomness materialized once; minimum-participation and failed-checkpoint signing refusals retained; durable interrupted-round reservations; serialized stop/retry prevents obsolete retries. Signed checkpoints survive every coordinator failure until observed transaction/confirmed-conflict reconciliation. Credential waits observe cancellation; all issuance tasks cancel siblings on failure, and vanished rounds propagate promptly. Three new cancellation regressions fail against old behavior; all 325 WabiSabi host checks pass. Source-native 20/20 CI and local five-phase stop/blame/restart tests have separate retained evidence. |
| Transport | Public factories retain Tor SOCKS authentication isolation, onion RPC remains through Tor, public direct fallback absent, failure closes transport. Tor stop/restart test passes on the 16 KB emulator. |
| External fee information | Concurrent cache updates no longer collide, periodic updates fetch fresh information, and discarded retry responses are disposed. CPFP now selects the same official onion service already used by fee estimates when Tor is enabled, preserving HTTPS for direct desktop operation. Two route regressions fail before correction. Live Main/Testnet4 provider qualification remains a separate gate. |
| Settings publication | The Windows CI sharing failure was reproduced. Configuration instances share a normalized path lock; saves publish complete files atomically, initial creation completes before returning, and read failures propagate without overwriting usable settings. Corrupt UI decoding starts reactive persistence only after validation. Six settings and two safe-file regressions pass. |
| Blame qualification | The failing desktop fixture had registration and phase windows incompatible with Arena/client rules, unintended single-coin isolation, and a factory discarding the blame input minimum. Corrected fixture conditions retain production privacy and participation protections. Removed exception-to-success shortcut; three real blame rounds and all 325 WabiSabi checks pass locally. |
| Credential graph / allocation | A retained seed-23 production decomposition reproduces a zero-credential cycle. Routing excludes descendants across both credential types; the scheduler rejects incomplete/cyclic graphs before requests. Missing or reordered credentials cannot silently leave dependency promises unresolved. Literal, seeded-decomposition and generated-graph regressions are retained; the real blame fixture exercises seed 23. The original Mac trace did not retain exact graph values; current-source CI and Android rounds have separate evidence. |
| Android / package | Screenshots enabled by user request, disabled backups/debugging, separate instrumentation, sensitive inputs/clipboard, generation-aware Camera2 teardown, bounded decoding and foreground/wake-lock limits. Pure-Java testing exercises the actual signed APK's recorded capture policy. |
| Native metadata | Added documented common-page-size flag to the pinned SDK's generated app-library linker invocation, retaining RELRO/NOW/non-executable stack. Prebuilt binaries are not patched. |

## Open release findings

1. **Source runtime qualification:** the retained API 24 deadlock is traced to
   nested native handshake entry during certificate rejection. The scoped guard
   passes trust/hostname vectors. A separate missing public CA root is supplemented
   only on API 24/25 through full chain validation; negative signature, usage,
   expiry, hostname and unrelated-root checks pass. Public Tor stop/restart now
   passes API 24. At clean `24abf94c`, every API 24, 35, 36/4 KB and 36/16 KB
   Debug/Release job passed Android CI `37285067882`, using OpenSSL 3.5.9.
   Subsequent changes and corrected ARM64 handset execution need
   separate results; an old pass is not attributed to a new build.
2. **Strict native 16 KB qualification:** static RELRO-end checks flag the old prebuilt
   .NET runtime/Tor libraries and ARM64 SQLite, including after the Mono 10.0.12
   servicing update. The package inspector records them
   and exits nonzero. Successful x64 16 KB execution does not clear the findings or
   establish ARM64 safety. Required pinned source rebuilds pass all 38 native ELF
   checks, including packaging hashes and SDK/loader container-layout verification.
   Preliminary version-5 personal package inspection passes all 20 native payloads
   and 438 managed payloads. Inspect the final clean-source package and qualify
   corrected ARM64 execution while retaining original security flags.
3. **Remaining qualification:** five phase-specific safe stops, actual signing
   dropout/blame and coordinator restart pass locally. Actual confirmation
   withholding passed in `device-ce573f1f71264385b28a1a7cfb280c2f` and process-death
   recovery, a fresh recovered signing spend, and original-wallet confirmed-conflict
   reconciliation passed in `device-e588d357cc41484bbcba1fb0a0b9884d`. The newest
   nine-scenario CI and 20 consecutive fresh rounds passed at `24abf94c`.
   Production Main/Testnet4 CPFP also passed through the official onion service
   in `device-ed11374569524885a226a04140a88999`. Fresh public synchronization,
   current-source desktop blame qualification and final personal APK checks remain
   separate gates; delivery JSON records their
   actual final outcomes. Uncertain signed inputs remain reserved until observed
   or reconciled. Retained timeout/startup failures are not silently waived.
   Public Testnet4 diagnosis found peers without compact-filter support; discovery
   now queries the existing seeds' service-filtered answers through the same
   resolver, while retaining general answers, service handshakes and netgroup
   limits. Three regressions pass, with their pre-correction failures retained.
4. **Remaining handset evidence:** the corrected source-native crypto, SQLite and
   TLS probe passed at clean `76ab3fa9` on the ARM64 Android 16 phone. Version 11
   was installed with the personal certificate and a new disposable wallet opened.
   Hardware-backed per-use authentication and
   cancellation, real camera scanning/rotation, wallet recovery, accessibility and
   updates still require qualification. No funded user seed or real-bitcoin
   transaction was used.
5. **Latest transaction rejections:** `41f878af` API 36 jobs rejected a
   cancellation and a CPFP child while retaining their uncertain bytes and
   reservations. Generic error handling discarded Core's specific reasons.
   Signed-size and non-final locktime defects now have deterministic reproductions
   and corrections; the original failures cannot be assigned a specific cause
   from those older logs alone. New diagnostics retain the node reason without
   exporting fixture wallets or journals. The final matrix must pass and any
   further rejection must be resolved before release acceptance.

This review found and corrected the concrete implementation issues listed above.
It does not certify absence of other critical/high findings. No release-ready
claim is allowed while runtime/package findings or required phone gates remain.

## Public synchronization findings

A partial P2P filter response could lower the reported target to the currently
downloaded block-header height, and a response prepared before a new peer
announcement could erase that newer target. Atomic P2P advancement now preserves
those targets; authoritative RPC reorganization handling can still lower them.
Retained negative tests cover both races before correction.

A stalled compact-filter send also held its behavior lock synchronously, blocking
timeout/disconnect cleanup and the shared ticker. Local transport fault injection
reproduces that class of stall. Bounded asynchronous sends observe late failures
and cancel on detach. The retained public height-69501 stall lacks a complete
managed stack, so its unique cause is not inferred solely from the successful
controlled regression. New actual public-network execution is required.

Tor startup was prematurely cancelled after three minutes even when cold descriptor
and circuit bootstrap was progressing. The new monotonic progress deadline keeps
three-minute idle and ten-minute absolute bounds, with no transport fallback.
Wake locks now cover startup/reconnection and wallets behind the filter height,
while preserving foreground-service shutdown and timeout handling. Physical handset
background behavior remains a separate gate.

The selected desktop coordinator currently redirects `/wabisabi/status` to a
homepage. Production CoinJoin availability is blocked; no synthetic round count
establishes external service availability.

Header transport has a separate actual-wire reproduction: a handshaked peer
announcing a higher height can withhold headers indefinitely. A two-minute
monotonic validated-progress bound now disconnects and scores that peer. Empty
responses cannot reset it; invalid proof of work causes immediate retirement;
caught-up peers and disabled synchronization remain available. All six regressions
and the full 1,149-unit suite pass, with the old failing timeout retained. This
does not uniquely attribute the slower public mainnet fixture to one peer.

Biometric unlock/enrollment and replacement password work previously executed on
the Android interface thread. These operations now run in a worker, retaining
foreground/session/generation and cancellation checks before exposing the wallet
or using a resumed device grant. Ordinary unlocking reads and clears its native
password field on the interface thread. Hardware authentication and latest signed
package UI execution remain required; an earlier baseline System UI ANR is retained
without claiming a uniquely proven cause.

An additional actual-wire reproduction establishes that a removed peer's
unvalidated height could remain a permanent target. Two negative cases fail before
correction and pass with separate validated progress and live peer estimates.
Only the departed estimate is removed; active higher reports and validated
progress remain protected against partial-response races. Without validated
progress or a live report, cached filters alone cannot establish readiness.
Authoritative RPC reorganization updates retain their ability to lower the tip;
a later peer departure cannot erase that update. Handshake/disconnect races check
attachment and state again after registration. All 1,152 unit tests, including
nine actual transport cases, and all 60 mobile checks pass after correction.
Final Android execution is required separately from the earlier successful
version-10 UI/package results.

## Residual technical limits

The installed workload's initial Mono 10.0.9 was advanced to the stable 10.0.12
servicing runtime after an isolated cryptographic reproduction passed. Microsoft
published a high-severity TLS security advisory affecting older .NET versions:
[CVE-2026-50528](https://github.com/dotnet/runtime/security/advisories/GHSA-qvw7-jm5c-6hqw).
Its package table does not explicitly identify Android, so this review does not
assert confirmed Android exposure or attribute the API 24 stall to that advisory.
Using the stable servicing build alone did not close the reproduced stall or native
page findings. The subsequent pinned source builds and narrow TLS correction are
qualified separately above.

The original wallet format is encrypted with the original Wasabi password; device
credentials only decrypt an optional convenience copy. Recovery remains independent
of Android keys. RPC credentials are available in process memory to authorized
background networking. Managed immutable strings and third-party key objects cannot
be promised perfectly erased from all heap copies; references/caches are cleared,
owned signing keys are disposed, and byte buffers are zeroed where available.
A compromised OS, unlocked process or dependency can still expose a software wallet.

Local NuGet vulnerability feeds reported no package findings, but native components,
coordinator behavior and upstream cryptographic implementations were not independently
audited. Regtest round counts do not measure real-world anonymity. Preserve the
exact source/dependency/package manifest and all failed checks with delivery evidence.

## Native advisory assessment

Tor's independently pinned OpenSSL vendor was advanced from 3.5.8 to the supported
3.5.9 servicing commit `45e844fa2a14ec92d146bd8f5778ac130b6625fb`. The official
[29 September advisory](https://openssl-library.org/news/secadv/20260929.txt)
reports excessive allocation from crafted X.509 CRL distribution points during
normal TLS handshakes (CVE-2026-35189, low). This is relevant to client availability.
The same update fixes the high DTLS advisory; this wallet's Tor transport uses
TCP/TLS rather than DTLS. QUIC, CMP, SM2 and generic EC advisories are not used
by the wallet's Bitcoin signing code. Bitcoin derivation/signing remains in
NBitcoin; no Bitcoin algorithm was changed for this servicing update. Qualification
must record the rebuilt native hashes and actual Tor execution separately.
