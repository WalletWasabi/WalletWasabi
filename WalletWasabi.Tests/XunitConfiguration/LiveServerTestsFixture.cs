using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Helpers;

namespace WalletWasabi.Tests.XunitConfiguration;

public class LiveServerTestsFixture
{
	public Dictionary<Network, Uri> UriMappings { get; } = new Dictionary<Network, Uri>
	{
		{ Network.Main, new Uri(Constants.IndexerUri) },
		{ Network.TestNet, new Uri(Constants.TestnetIndexerUri) }
	};
}
