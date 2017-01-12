using NBitcoin;
using System.Collections.Generic;

namespace HiddenWallet
{
	public static class FakeData
	{
		public static class FakeSafe
		{
			private static readonly List<BitcoinAddress> FakeSafeAddresses = new List<BitcoinAddress>();

			static FakeSafe()
			{
				var addresses = new List<string>
				{
					"1CCXoPdEpWr2KZ1xJz5QHyUR3Nn8yFeCut",
					"1P2GWrCNJorPrHJdkHdtkAdEdUuWQq8uxg",
					"12D6kUKJm4jxYwRkMBavJepdznJToBKg9J",
					new Key().PubKey.GetAddress(Network).ToWif(),
					"1Jq67oPBoDcDAmNWVpv4MVFLz1RHkcgi9N",
					"1LuMj4J43iPp86ZwfnNTtNAKf9bpAqiNwh",
					"1gNT1YgsbaEEV2cUikcWL3puZHvwKs9ae",
					new Key().PubKey.GetAddress(Network).ToWif(),
					"1JvaeimvLV5B7yHNHBPUfBy1SM4H6rFazU",
					"1Jq8iGGT1idaK9z8H7mrYPynjh9LozHnSH",
					"1AcAxGyuMrgEga2daotAPWTYazt965o9rG",
					"1b2ryTRhoCDiCDndLk7LjsZRs8t2RSkET",
					new Key().PubKey.GetAddress(Network).ToWif(),
					"17tA7cXneTB8do1kWsYLbX6KUrkj99cGnL",
					"16kj374w6nvvSjTKdU1k7axP9bFghTNsfc",
					"1BSdeyWS627UMNHuwSv2diKjJFGrY6Hteo",
					new Key().PubKey.GetAddress(Network).ToWif(),
					"1Q3ZNxmXRjMFA2W3yiyujGNsfjdcoATemp",
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif(),
					new Key().PubKey.GetAddress(Network).ToWif()
				};
				var uniqueAddresses = new HashSet<string>();
				foreach (var addr in addresses)
					uniqueAddresses.Add(addr);
				foreach (var addr in uniqueAddresses)
					FakeSafeAddresses.Add(BitcoinAddress.Create(addr));
			}

			public static Network Network => Network.Main;

			public static BitcoinAddress GetAddress(int index)
			{
				return FakeSafeAddresses[index];
			}

			public static HashSet<BitcoinAddress> GetFirstNAddresses(int addressCount)
			{
				var addresses = new HashSet<BitcoinAddress>();
				for (var i = 0; i < addressCount; i++)
				{
					addresses.Add(GetAddress(i));
				}
				return addresses;
			}
		}
	}
}