//! payjoin-test-utils `TestServices` as a standalone binary, so a non-Rust test harness
//! (WalletWasabi.IntegrationTests/Payjoin) can spawn the exact directory + OHTTP relay + TLS
//! wiring payjoin's own e2e tests use: a self-signed localhost cert, a payjoin-mailroom
//! directory served over TLS, and N plain-HTTP relay instances whose outgoing clients trust
//! that cert.
//!
//! Stdout contract (line-oriented, parsed by the C# fixture):
//!   DIRECTORY_URL=https://localhost:<port>
//!   OHTTP_RELAY_URLS=http://localhost:<port>[,http://localhost:<port>...]
//!   CERT_DER_PATH=<path>
//!   READY
//! The process then serves until killed.

use std::path::PathBuf;

use payjoin_test_utils::TestServices;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    let mut relays: u8 = 2;
    let mut cert_out = PathBuf::from("cert.der");

    let mut args = std::env::args().skip(1);
    while let Some(arg) = args.next() {
        match arg.as_str() {
            "--relays" => {
                relays = args.next().ok_or("--relays needs a value")?.parse()?;
            }
            "--cert-out" => {
                cert_out = args.next().ok_or("--cert-out needs a value")?.into();
            }
            other => return Err(format!("unknown argument: {other}").into()),
        }
    }

    let services = TestServices::initialize_with_relays(relays).await?;
    services.wait_for_services_ready().await.map_err(|e| e.to_string())?;
    std::fs::write(&cert_out, services.cert())?;

    println!("DIRECTORY_URL={}", services.directory_url());
    println!("OHTTP_RELAY_URLS={}", services.ohttp_relay_urls());
    println!("CERT_DER_PATH={}", cert_out.display());
    println!("READY");

    // Serve until the harness kills the process.
    std::future::pending::<()>().await;
    unreachable!()
}
