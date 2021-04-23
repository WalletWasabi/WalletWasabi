using NBitcoin;
using System;

namespace WalletWasabi.WabiSabi.Backend.Models
{
	public class Alice
	{
		public Alice(Coin coin, byte[] signature)
		{
			// TODO init syntax?
			Coin = coin;
			Signature = signature;
		}

		public Guid Id { get; } = Guid.NewGuid();
		public DateTimeOffset Deadline { get; set; } = DateTimeOffset.UtcNow;
		public Coin Coin { get; }
		public byte[] Signature { get; }
		public Money TotalInputAmount => Coin.Amount;
		public int TotalInputVsize => Coin.ScriptPubKey.EstimateInputVsize();

		public bool ConfirmedConnection { get; set; } = false;

		public long CalculateRemainingVsizeCredentials(uint maxRegistrableSize) => maxRegistrableSize - TotalInputVsize;

		public Money CalculateRemainingAmountCredentials(FeeRate feeRate) => TotalInputAmount - feeRate.GetFee(TotalInputVsize);

		public void SetDeadlineRelativeTo(TimeSpan connectionConfirmationTimeout)
		{
			// Have alice timeouts a bit sooner than the timeout of connection confirmation phase.
			Deadline = DateTimeOffset.UtcNow + (connectionConfirmationTimeout * 0.9);
		}
	}
}
