using Xunit;

namespace WalletWasabi.Tests.XunitConfiguration
{
	[CollectionDefinition("LiveServerTests collection")]
	public class LiverServerTestsCollections : ICollectionFixture<LiveServerTestsFixture>
	{
	}
}
