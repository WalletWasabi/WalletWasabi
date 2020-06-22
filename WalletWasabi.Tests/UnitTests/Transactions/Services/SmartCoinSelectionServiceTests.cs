using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Blockchain.Transactions.Services;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions
{
	public class SmartCoinSelectionServiceTests
	{
		[Fact]
		public void GetAllowedSmartCoinInputsTest()
		{
			SmartCoin c1 = MakeSmartCoin(label: "c1", pubKey: NewKey(), amount: 100000000, confirmed: true);
			SmartCoin c2 = MakeSmartCoin(label: "c2", pubKey: NewKey(), amount: 20000000, confirmed: true);

			List<SmartCoin> inputCoins = new List<SmartCoin>() { c1, c2 };
			var coinsView = new CoinsView(inputCoins);

			var service = new SmartCoinSelectionService(allowUnconfirmed: false);
			List<SmartCoin> smartCoins1 = service.GetAllowedSmartCoinInputs(coinsView);
			Assert.Equal(inputCoins.Count, smartCoins1.Count);

			Assert.Equal(inputCoins[0], smartCoins1[0]);
			Assert.Equal(inputCoins[1], smartCoins1[1]);

			List<OutPoint> allowedInputs = new List<OutPoint>();
			List<SmartCoin> smartCoins2 = SmartCoinSelectionService.IntersectWithAllowedInputs(smartCoins1, allowedInputs);

			Assert.Empty(smartCoins2);
		}

		private static SmartCoin MakeSmartCoin(string label, HdPubKey pubKey, decimal amount, bool confirmed = true, int anonymitySet = 1)
		{
			var randomIndex = new Func<int>(() => new Random().Next(0, 200));
			var height = confirmed ? new Height(randomIndex()) : Height.Mempool;
			SmartLabel slabel = label;
			var spentOutput = new[]
			{
				new OutPoint(RandomUtils.GetUInt256(), (uint)randomIndex())
			};
			pubKey.SetLabel(slabel);
			pubKey.SetKeyState(KeyState.Used);
			return new SmartCoin(RandomUtils.GetUInt256(), (uint)randomIndex(), pubKey.P2wpkhScript, Money.Coins(amount), spentOutput, height, replaceable: false, anonymitySet, slabel, pubKey: pubKey);
		}

		private static (string, KeyManager) DefaultKeyManager()
		{
			var password = "blahblahblah";
			return (password, KeyManager.CreateNew(out var _, password));
		}

		private static HdPubKey NewKey()
		{
			var (password, keyManager) = DefaultKeyManager();

			keyManager.AssertCleanKeysIndexed();
			return keyManager.GenerateNewKey("", KeyState.Used, true, false);
		}
	}
}
