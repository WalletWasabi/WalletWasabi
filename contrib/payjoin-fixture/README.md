# payjoin-fixture

`payjoin-test-utils::TestServices` as a standalone binary for the payjoin integration
harness (`WalletWasabi.IntegrationTests/Payjoin/`, xunit category `PayjoinHarness`).

The standalone `payjoin-mailroom` binary cannot reproduce the TLS wiring payjoin's own
e2e tests use: `serve_manual_tls` is a library function behind the private `_manual-tls`
feature that its `main()` never calls, and its relay's outgoing HTTP client trusts webpki
roots only, so it would reject a self-signed local directory certificate. This shim is
the thinnest possible bridge: it starts `TestServices` (self-signed localhost cert, one
mailroom directory over TLS, N plain-HTTP relay instances whose outgoing clients trust
that cert), writes the cert DER to disk, prints the service URLs, and serves until
killed. It should be deleted once payjoin-mailroom grows a manual-TLS fixture mode.

## Build

```bash
# In the rust-payjoin #csharp devShell (provides rust and sets BITCOIND_SKIP_DOWNLOAD=1,
# which the corepc-node transitive dependency's build script requires offline):
nix develop <rust-payjoin>#csharp --command \
  cargo build --manifest-path contrib/payjoin-fixture/Cargo.toml
```

## Dependency pinning

`Cargo.toml` uses a **path dependency** on a local rust-payjoin checkout
(upstream master `d27b7b137c9e8696ad6bf542ba0c1bf93665df72`) expected as a sibling of this
repository — the dev-reproducible default. For a build that needs no local checkout, swap to
the git form (one-line change, documented inline in Cargo.toml):

```toml
payjoin-test-utils = { git = "https://github.com/payjoin/rust-payjoin", rev = "d27b7b137c9e8696ad6bf542ba0c1bf93665df72" }
```

## Stdout contract

```
DIRECTORY_URL=https://localhost:<port>
OHTTP_RELAY_URLS=http://localhost:<port>,http://localhost:<port>
CERT_DER_PATH=<path given via --cert-out>
READY
```

Flags: `--relays <n>` (default 2), `--cert-out <path>` (default `./cert.der`).
The process serves until killed; the C# fixture owns its lifetime.
