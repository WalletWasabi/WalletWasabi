using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace WalletWasabi.Tests.XunitConfiguration
{
	[CollectionDefinition("LiveServerTests collection")]
	public class LiverServerTestsCollections : ICollectionFixture<LiveServerTestsFixture>
	{
	}
}
