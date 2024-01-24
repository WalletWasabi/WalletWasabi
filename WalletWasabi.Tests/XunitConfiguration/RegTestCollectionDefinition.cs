using Xunit;

namespace WalletWasabi.Tests.XunitConfiguration;

/// <summary>
/// The tests in a test collection denoted using this collection cannot run in parallel because only one instance of bitcoind is running for a set of tests.
/// </summary>
/// <remarks>This class has no code, and is never created. Its purpose is simply to be the place to apply <see cref="CollectionDefinitionAttribute"/>.</remarks>
/// <seealso href="https://xunit.net/docs/shared-context#class-fixture"/>
[CollectionDefinition("RegTest collection", DisableParallelization = true)]
public class RegTestCollectionDefinition
{
}
