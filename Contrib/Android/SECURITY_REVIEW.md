# Android source security review

This is a source review by the implementing agent, recorded 2026-10-05. It is
**not independent, not a penetration test, and not an audit or guarantee**.
Release acceptance is blocked by the findings and missing device evidence below.

## Reviewed boundaries and corrections

| Boundary | Correction / verification |
| --- | --- |
| Mainnet in development | Per-channel session assembly rejects mainnet even if a caller supplies the personal policy. Package/data identities are separate. |
| Secret persistence | Settings exclude RPC plaintext. Android Keystore AEAD binds encrypted values to package/reference; per-use wallet retrieval requires hardware-backed authentication, otherwise password-only. Vault tamper/key-loss tests pass for RPC storage. |
| Authorization and lock | Fresh payment/replacement/backup/CoinJoin authorization; secrets released after non-CoinJoin use; stop releases retained CoinJoin secrets while locked. Cancelled/queued biometric callbacks retain Java peer lifetime instead of being disposed early. UI lock generations invalidate delayed completions; busy operations and recovery-word screens no longer bypass inactivity locking. Hardware authorization still needs the phone. |
| Review versus signing | Immutable public details, private PSBT snapshot, wallet/network binding, monotonic five-minute expiry and serialized confirmation. The transaction factory's unsigned Signed flag was corrected and covered by a regression. |
| Ambiguous broadcast | Exact signed bytes are durably journaled before broadcast, inputs stay reserved, duplicate confirmation returns the same receipt, reconciliation never constructs a new payment. Failed journal writes conservatively retain in-memory reservations and never broadcast. |
| Replacement | Mobile RBF preserves every foreign recipient address/amount. Eligible CPFP is attempted when RBF cannot preserve them; unavailable cases fail. Desktop defaults remain signed and retain existing policy. Funded RBF/cancel and CPFP passed; CPFP's production fee service remains unqualified. |
| Reorganization / synchronization | RPC filter hashes come from the RPC chain; batch parsing is awaited, transport failure retries without rollback, and a same-height replacement is detected. Five regression cases failed before correction; 15 related host tests and funded Android reorganization/reconfirmation pass. |
| Recovery / address allocation | Both account public keys checked, import scan height reset, atomic complete-file publication with flush, directory flush on Android/Linux, and address persistence before disclosure. Fresh-directory encrypted recovery and restored signing passed. |
| CoinJoin | Denomination randomness materialized once; minimum-participation and failed-checkpoint signing refusals retained; durable interrupted-round reservations; request versions and serialized stop/retry transitions prevent an obsolete retry from superseding a stop. Signed checkpoints now survive every coordinator-reported failure until transaction/confirmed-conflict reconciliation; six regressions failed before the fix and pass afterward. Fresh 20/20 Release rounds passed locally and in remote CI on Mono 10.0.12; source native rebuilds need separate qualification. |
| Transport | Public factories retain Tor SOCKS authentication isolation, onion RPC remains through Tor, public direct fallback absent, failure closes transport. Tor stop/restart test passes on the 16 KB emulator. |
| Android / package | Secure windows, disabled backups/debugging, separate instrumentation, sensitive inputs/clipboard, generation-aware Camera2 teardown, bounded decoding and foreground/wake-lock limits. Pure-Java testing exercises the actual signed APK without security exemptions. |
| Native metadata | Added documented common-page-size flag to the pinned SDK's generated app-library linker invocation, retaining RELRO/NOW/non-executable stack. Prebuilt binaries are not patched. |

## Open release findings

1. **Source runtime qualification:** the retained API 24 deadlock is traced to
   nested native handshake entry during certificate rejection. The scoped guard
   passes trust/hostname vectors. A separate missing public CA root is supplemented
   only on API 24/25 through full chain validation; negative signature, usage,
   expiry, hostname and unrelated-root checks pass. Public Tor stop/restart now
   passes API 24. The full rebuilt runtime/architecture matrix remains open.
2. **Strict native 16 KB qualification:** static RELRO-end checks flag the old prebuilt
   .NET runtime/Tor libraries and ARM64 SQLite, including after the Mono 10.0.12
   servicing update. The package inspector records them
   and exits nonzero. Successful x64 16 KB execution does not clear the findings or
   establish ARM64 safety. Pinned source rebuilds pass all 38 native ELF checks,
   with packaging hashes and SDK/loader container-layout verification. Qualify the
   full final package and ARM64 execution while retaining original security flags.
3. **Incomplete disruption matrix:** host protocol tests and successful emulator
   rounds are narrower evidence than Android coordinator restart, blame/dropout,
   process death during signing, every stop race, production CPFP fee service and long background-budget
   qualification. Uncertain signed-round inputs remain reserved until observed or
   reconciled; manual availability recovery is not proven.
4. **Remaining handset evidence:** the isolated stock-runtime Release crypto probe
   passed on the ARM64 Android 16 phone. Hardware-backed per-use authentication and
   cancellation, real camera scanning/rotation, wallet recovery, accessibility and
   updates still require qualification. No funded user seed or real-bitcoin
   transaction was used.

This review found and corrected the concrete implementation issues listed above.
It does not certify absence of other critical/high findings. No release-ready
claim is allowed while runtime/package findings or required phone gates remain.

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
