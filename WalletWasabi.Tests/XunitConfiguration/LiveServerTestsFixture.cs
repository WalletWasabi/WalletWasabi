using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Models;

namespace WalletWasabi.Tests.XunitConfiguration
{
	public class LiveServerTestsFixture : IDisposable
	{
		public Dictionary<NetworkType, Uri> UriMappings { get; internal set; }
		public Dictionary<NetworkType, (BitcoinPubKeyAddress activeOutputAddress, BitcoinPubKeyAddress changeOutputAddress, string blindedDataHex, string proof, List<TxoRef> utxos)> AliceInputMappings { get; internal set; }

		public LiveServerTestsFixture()
		{
			UriMappings = new Dictionary<NetworkType, Uri>
			{
					{ NetworkType.Mainnet, new Uri("http://4jsmnfcsmbrlm7l7.onion") },
					{ NetworkType.Testnet, new Uri("http://wtgjmaol3io5ijii.onion") }
			};

			AliceInputMappings = new Dictionary<NetworkType, (BitcoinPubKeyAddress activeOutputAddress, BitcoinPubKeyAddress changeOutputAddress, string blindedDataHex, string proof, List<TxoRef> utxos)>
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
				},
				{
					NetworkType.Mainnet,
					(
					new BitcoinPubKeyAddress("1KVEojQUY2DZ7XjC5xdeL6tt4thfdNFVgv"),
					new BitcoinPubKeyAddress("13Xwp6pM3WQCYqrM9otaatau1UUVGRZZm8"),
					"45E6926EC927C7BDF27F1BFC30049E2E6135A844901BABE25352E02B6B7F0B5C7562ACC15243E6869BC366987A85B612FBA9EC3E7C5D72258477E68C5E995D3B3EBB47A6688DFC610E5E315ED8DD4AFFF2CCBEF74DBC85772240371CD60305E8AF2F0B3E0E26716A07536FD31B1C38BD6C4D08906CEFD41F2A2C81A45AD2C9789B6251882CD432BDB49F2EF5E9C7388A47E9AE8BDA88713DF1C68BA760C58BA45A0F50FAEF2E19B91FB2076936E550F227932642479CBCAE5C237F14CC0FF5C761596F726852DAE8B4338963151BC6C644066C6B5BC092D5CB5EF2FE904715255D855D624FF6889D2E05E15BA7E8442758ED447201990AD930B3FDF2A9A7306E",
					"IB+DA52LnSiI3PlYSH+A9QRkwK1ULrauAMKvzZ1nmeOmDzGSw2bMq0lyYd7WvJb8xXyx1uvTfi1BxY/Cy6snvdw=",
					new List<TxoRef>
					{
						new TxoRef(new uint256("d4b38544efc7d460f64f3d3446e9a3125c425cccfa25671c6b11f965729c8d75"), 0)
					})
				}
			};
		}

		public (BitcoinPubKeyAddress activeOutputAddress, BitcoinPubKeyAddress changeOutputAddress, string blindedDataHex, string proof, List<TxoRef> utxos) GetAliceInputData(NetworkType networkType)
		{
			return AliceInputMappings[networkType];
		}

		public void Dispose()
		{
			//
		}
	}
}
