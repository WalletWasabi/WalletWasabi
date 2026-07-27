using System;
using Xunit;

namespace WalletWasabi.IntegrationTests.Payjoin;

/// <summary>
/// Proves the PayjoinHarness CI exclusion stays effective. This test is deliberately outside the
/// harness collection (no fixtures, instant) and fails if it ever executes inside a nix build
/// sandbox — i.e. if the <c>Category!=PayjoinHarness</c> filter in flake.nix's checkPhase is
/// removed or broken, <c>nix build .#all</c> goes red on this test before any harness fixture
/// can hang the sandboxed run.
/// </summary>
public class PayjoinHarnessSandboxCanary
{
	[Fact]
	[Trait("Category", "PayjoinHarness")]
	public void HarnessCategoryIsExcludedFromNixSandbox()
	{
		// NIX_BUILD_TOP is set both in the build sandbox and by `nix develop`; IN_NIX_SHELL is
		// only set by `nix develop`. Fail only in the sandbox.
		bool inNixBuildSandbox = Environment.GetEnvironmentVariable("NIX_BUILD_TOP") is not null
			&& Environment.GetEnvironmentVariable("IN_NIX_SHELL") is null;

		Assert.False(
			inNixBuildSandbox,
			"PayjoinHarness-category tests must not run inside the nix build sandbox: they spawn payjoin-cli/payjoin-mailroom/bitcoind, which are not provisioned there. Restore the Category!=PayjoinHarness filter in flake.nix checkPhase.");
	}
}
