using Xunit;

namespace WalletWasabi.Tests.XunitConfiguration;

/// <summary>
/// The tests in a test collection denoted using this collection definition are time-sensitive, therefore the test collection is run in a special way:
/// Parallel-capable test collections will be run first (in parallel), followed by parallel-disabled test collections (run sequentially) like this one.
/// </summary>
/// <remarks>This class has no code, and is never created. Its purpose is simply to be the place to apply <see cref="CollectionDefinitionAttribute"/>.</remarks>
/// <seealso href="https://xunit.net/docs/shared-context#collection-fixture"/>
/// <seealso href="https://xunit.net/docs/running-tests-in-parallel.html#parallelism-in-test-frameworks"/>
[CollectionDefinition("Serial unit tests collection", DisableParallelization = true)]
public class SerialCollectionDefinition
{
}
