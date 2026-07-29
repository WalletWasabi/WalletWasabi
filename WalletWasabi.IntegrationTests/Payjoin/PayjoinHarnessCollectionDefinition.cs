using Xunit;

namespace WalletWasabi.IntegrationTests.Payjoin;

/// <summary>
/// Payjoin harness tests share one bitcoind + mailroom directory/relay environment and must run
/// sequentially. The whole collection carries the PayjoinHarness category and is excluded from
/// the sandboxed nix checkPhase (flake.nix filters out the PayjoinHarness trait): the nix sandbox has
/// no network and none of the harness binaries provisioned.
/// </summary>
[CollectionDefinition("Payjoin harness", DisableParallelization = true)]
public class PayjoinHarnessCollectionDefinition : ICollectionFixture<PayjoinHarnessFixture>
{
}
