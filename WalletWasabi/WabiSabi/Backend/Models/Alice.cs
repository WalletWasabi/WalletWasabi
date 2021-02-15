using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Models
{
	public class Alice
	{
		public Alice(IDictionary<Coin, byte[]> coinRoundSignaturePairs)
		{
			Coins = coinRoundSignaturePairs.Keys;
			CoinRoundSignaturePairs = coinRoundSignaturePairs;
		}

		public Guid Id { get; } = Guid.NewGuid();
		public IEnumerable<Coin> Coins { get; }
		public IDictionary<Coin, byte[]> CoinRoundSignaturePairs { get; }
	}
}
