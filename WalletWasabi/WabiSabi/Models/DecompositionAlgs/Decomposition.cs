using NBitcoin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models.DecompositionAlgs
{
	public class Decomposition : IEnumerable<Coin>
	{
		private List<Coin> Coins { get; } = new();

		public void Extend(Coin coin)
		{
			Coins.Add(coin);
			Coins.Sort();
		}

		public Money FaceValue()
		{
			return Coins.Sum(c => c.Amount);
		}

		public int OutputsVbytes()
		{
			return Coins.Sum(_ => Coin.OutputVbytes);
		}

		public decimal InputsVbytes()
		{
			return Coins.Sum(_ => Coin.InputVbytes);
		}

		public Money EffectiveValue(FeeRate feeRate)
		{
			var sats = (long)Math.Floor(feeRate.SatoshiPerByte * Coin.InputVbytes * Coins.Count);
			return FaceValue() - Money.Satoshis(sats);
		}

		public Money EffectiveCost(FeeRate feeRate)
		{
			var sats = (long)Math.Floor(feeRate.SatoshiPerByte * Coin.OutputVbytes * Coins.Count);
			return FaceValue() + Money.Satoshis(sats);
		}

		public IEnumerator<Coin> GetEnumerator()
		{
			return Coins.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return Coins.GetEnumerator();
		}
	}
}
