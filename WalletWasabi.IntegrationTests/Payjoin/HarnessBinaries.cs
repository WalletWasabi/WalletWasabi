using System;
using System.IO;
using WalletWasabi.BundledApps;
using WalletWasabi.Helpers;

namespace WalletWasabi.IntegrationTests.Payjoin;

/// <summary>
/// Resolves the external binaries the payjoin harness spawns. These are NOT bundled with the
/// repository: payjoin-cli and payjoin-mailroom are built from a local rust-payjoin checkout
/// (upstream master d27b7b137c9e8696ad6bf542ba0c1bf93665df72) located via PAYJOIN_RUST_DIR, and bitcoind
/// comes from the BITCOIND_EXE environment variable (set by the rust-payjoin nix devShells)
/// because the bundled generic-linux bitcoind cannot exec on NixOS hosts.
/// Tests using these binaries carry <c>[Trait("Category", "PayjoinHarness")]</c> and are excluded
/// from the sandboxed <c>nix build .#all</c> checkPhase, which has no network and none of these
/// binaries provisioned.
/// </summary>
public static class HarnessBinaries
{
	// Root of a local rust-payjoin checkout providing payjoin-cli / payjoin-mailroom, from
	// PAYJOIN_RUST_DIR. Leave it unset and point PAYJOIN_CLI_BIN / PAYJOIN_MAILROOM_BIN at the
	// built binaries directly.
	private static string? RustDir =>
		Environment.GetEnvironmentVariable("PAYJOIN_RUST_DIR") is { Length: > 0 } dir ? dir : null;

	private const string BuildInstruction =
		"Build them from a rust-payjoin checkout: nix develop <rust-payjoin>#csharp --command " +
		"cargo build -p payjoin-cli --features _manual-tls,v1 -p payjoin-mailroom.";

	public static string PayjoinCliPath => Resolve("PAYJOIN_CLI_BIN", "payjoin-cli");

	public static string MailroomPath => Resolve("PAYJOIN_MAILROOM_BIN", "payjoin-mailroom");

	/// <summary>The in-repo TestServices shim (contrib/payjoin-fixture); provides the TLS topology.</summary>
	public static string PayjoinFixturePath
	{
		get
		{
			string repoRoot = Path.GetFullPath(Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "..", "..", "..", ".."));
			string path = Environment.GetEnvironmentVariable("PAYJOIN_FIXTURE_BIN") is { Length: > 0 } fromEnv
				? fromEnv
				: Path.Combine(repoRoot, "contrib", "payjoin-fixture", "target", "debug", "payjoin-fixture");
			if (!File.Exists(path))
			{
				throw new FileNotFoundException(
					$"'payjoin-fixture' not found at '{path}' (override with PAYJOIN_FIXTURE_BIN). Build it: nix develop <rust-payjoin>#csharp --command cargo build --manifest-path {Path.Combine(repoRoot, "contrib", "payjoin-fixture", "Cargo.toml")}",
					path);
			}

			return path;
		}
	}

	/// <summary>The bundled bitcoind cannot exec on NixOS, so prefer BITCOIND_EXE when set.</summary>
	public static string BitcoindPath
	{
		get
		{
			string? fromEnv = Environment.GetEnvironmentVariable("BITCOIND_EXE");
			if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
			{
				return fromEnv;
			}

			return BundledAppHelpers.GetBinaryPath("bitcoind");
		}
	}

	private static string Resolve(string envVar, string binName)
	{
		string? path = Environment.GetEnvironmentVariable(envVar) is { Length: > 0 } fromEnv
			? fromEnv
			: RustDir is { } dir ? Path.Combine(dir, "target", "debug", binName) : null;
		if (path is null || !File.Exists(path))
		{
			throw new FileNotFoundException(
				$"'{binName}' not found (set {envVar} to the binary, or PAYJOIN_RUST_DIR to a rust-payjoin checkout). {BuildInstruction}",
				path ?? binName);
		}

		return path;
	}
}
