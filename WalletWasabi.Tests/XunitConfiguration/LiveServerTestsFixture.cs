using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Tests.XunitConfiguration
{
	public class LiveServerTestsFixture : IDisposable
	{
		public LiveServerTestsFixture()
		{
			UriMappings = new Dictionary<NetworkType, Uri>
			{
					{ NetworkType.Mainnet, new Uri("http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion") },
					{ NetworkType.Testnet, new Uri("http://testwnp3fugjln6vh5vpj7mvq3lkqqwjj3c2aafyu7laxz42kgwh2rad.onion") }
			};
		}

		public Dictionary<NetworkType, Uri> UriMappings { get; internal set; }

		public void Dispose()
		{
		}
	}
}
