using Xunit;

namespace WalletWasabi.Tests.XunitConfiguration
{
	[CollectionDefinition("RegTest collection")]
	public class RegTestCollection : ICollectionFixture<RegTestFixture>
	{
		// This class has no code, and is never created. Its purpose is simply
		// to be the place to apply [CollectionDefinition] and all the
		// ICollectionFixture<> interfaces.
	}

	[CollectionDefinition(nameof(LiveServerTests) + " collection")]
	public class LiverServerTestsCollections : ICollectionFixture<LiveServerTestsFixture>
	{
	}
}
