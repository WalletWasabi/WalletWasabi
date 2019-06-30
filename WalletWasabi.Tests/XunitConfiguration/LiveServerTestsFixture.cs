using NBitcoin;
using System;
using System.Collections.Generic;
using WalletWasabi.Models;

namespace WalletWasabi.Tests.XunitConfiguration
{
	public class LiveServerTestsFixture : IDisposable
	{
		public Dictionary<NetworkType, Uri> UriMappings { get; internal set; }

		public LiveServerTestsFixture()
		{
			UriMappings = new Dictionary<NetworkType, Uri>
			{
					{ NetworkType.Mainnet, new Uri("http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion") },
					{ NetworkType.Testnet, new Uri("http://testwnp3fugjln6vh5vpj7mvq3lkqqwjj3c2aafyu7laxz42kgwh2rad.onion") }
			};
		}

		public (BitcoinPubKeyAddress activeOutputAddress, BitcoinPubKeyAddress changeOutputAddress, string blindedDataHex, string proof, List<TxoRef> utxos) GetAliceInputData(NetworkType networkType)
		{
			var aliceInputMappings = new Dictionary<NetworkType, (BitcoinPubKeyAddress activeOutputAddress, BitcoinPubKeyAddress changeOutputAddress, string blindedDataHex, string proof, List<TxoRef> utxos)>
			{
				{
					NetworkType.Testnet,
					(
					new BitcoinPubKeyAddress("mrEadRbqnbFm7gvqFRisUvqh31tMPy9EGJ"),
					new BitcoinPubKeyAddress("mqjeEPQuPVEacfAgfnkhtkbFAYjxRrLUre"),
					"546B0901BF3CBA6A694B89A2914C3970FB3BA45EA4E917D049F6A4B5DF84E39B84EB35FF2295F65DE10C95E0280F78B8C4177132807C14A9F0C0358614BA5708F119F161AE05199AC465EF734C2C77D0719AA10BA03F9541E81FF6E80587AAA27B09962BF44FBAA6C2FD3F4C6E2778D5220ED522D1902F73CEC8627E08DE9062E7DF815F1DBD538C801211C0A602938851FC38C2DC9166F51904340386067E5983189249B2420D2CA0838AE2EA3F4AD2445B00245F2F5AF1C34CAEC403C8B1BDAB46F78BFF60F939AB5441A36A31E0EE3D4BF8E46DD67B036AC009A053BCAD7CF0DA1482239DA3559150E0620011A198AE3215ADC3C2F6E50B85767BA61AB1A9",
					"H+qI1XYOKWL1x1MQjEzHWkKIX2o3pKzSauY99e7rncTCUhIPwHYvL0END6gVs7rhoXZHgVB0IZOipZYtCAkbMtM=",
					new List<TxoRef>
					{
						new TxoRef(new uint256("32914e4466c2ba0328bcac6102d5a806b3e44d5ed73b2454ecae730bebcf8784"), 0),
						new TxoRef(new uint256("ac0e02035e885fe0ea921f7a7bbbdb362378ed0800fbe0e54af676a8ab0df710"), 0),
						new TxoRef(new uint256("72b8b61f9bb57519cda99458f4b3fb9881142492b9c55ce50ec70bde42f419ed"), 0),
						new TxoRef(new uint256("ec123d68e3cafcd648ae5258f1587afb367eb6a3bc4275d50fcacbad9e27b12e"), 0),
						new TxoRef(new uint256("6451fbf9b39e4ca649a36e71381b45e314526aa0074dc055eab39de581aeedfb"), 0),
						new TxoRef(new uint256("b65a812be86078ac5b5d2a531aee6197a628d60034dbde9cf035d5a5d8bd064f"), 0)
					})
				}
			};

			return aliceInputMappings[networkType];
		}

		public void Dispose()
		{
			//
		}
	}
}
