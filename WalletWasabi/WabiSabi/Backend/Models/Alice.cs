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
		public DateTimeOffset Deadline { get; private set; } = DateTimeOffset.UtcNow;
		public IEnumerable<Coin> Coins { get; }
		public IDictionary<Coin, byte[]> CoinRoundSignaturePairs { get; }
		public Money TotalInputAmount => Coins.Sum(x => x.Amount);
		public long TotalInputWeight => Coins.Sum(x => x.ScriptPubKey.EstimateSpendVsize() * 4);

		public long CalculateRemainingWeightCredentials(uint maxRegistrableWeight) => maxRegistrableWeight - TotalInputWeight;

		public void SetDeadlineRelativeTo(TimeSpan connectionConfirmationTimeout)
		{
			// Have alice timeouts a bit sooner than the timeout of connection confirmation phase.
			Deadline = DateTimeOffset.UtcNow + (connectionConfirmationTimeout * 0.9);
		}
	}
}
