using NBitcoin;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WalletWasabi.KeyManagement;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Transactions;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class CoinsRegistryTests
	{
		[Fact]
		public void OnlyUnspentCoinsAreListed()
		{
			var registry = new CoinsRegistry();
			var spentCoin = Coin(0.2m);
			registry.TryAdd(Coin(0.1m));
			registry.TryAdd(spentCoin);
			registry.TryAdd(Coin(0.4m));

			registry.Spend(spentCoin);

			Assert.Equal(2, registry.Count());
		}

		private static SmartCoin Coin(decimal amount, bool confirmed = true, int anonymitySet = 1)
		{
			var randomIndex = new Func<int>(() => new Random().Next(0, 200));
			var height = confirmed ? new Height(randomIndex()) : Height.Mempool;
			var slabel = new SmartLabel("");
			var spentOutput = new[]
			{
				new TxoRef(RandomUtils.GetUInt256(), (uint)randomIndex())
			};
			var pubKey = new HdPubKey(new Key().PubKey, KeyPath.Parse("m/84h/0h/0h/0/1"), slabel, KeyState.Used);
			return new SmartCoin(RandomUtils.GetUInt256(), (uint)randomIndex(), pubKey.P2wpkhScript, Money.Coins(amount), spentOutput, height, false, anonymitySet, false, slabel, pubKey: pubKey);
		}

	}
}
