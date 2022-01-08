using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.Tests.XunitConfiguration;

public class LiveServerTestsFixture : IDisposable
{
	public LiveServerTestsFixture()
	{
		UriMappings = new Dictionary<Network, Uri>
			{
					{ Network.Main, new Uri("http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion") },
					{ Network.TestNet, new Uri("http://testwnp3fugjln6vh5vpj7mvq3lkqqwjj3c2aafyu7laxz42kgwh2rad.onion") }
			};
	}

	public Dictionary<Network, Uri> UriMappings { get; internal set; }

	public void Dispose()
	{
	}
}
