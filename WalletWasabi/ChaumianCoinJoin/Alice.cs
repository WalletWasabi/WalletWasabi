using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.ChaumianCoinJoin
{
    public class Alice
    {
		public DateTimeOffset LastSeen { get; set; }

		public Guid UniqueId { get; }

		public Money InputSum { get; }

		public Money FeeToPay { get; }

		public Money OutputSumWithoutCoordinatorFeeAndDenomination { get; }

		public Dictionary<OutPoint, TxOut> Inputs { get; }

		public Script ChangeOutputScript { get; }

		public Money GetChangeAmount(Money denomination, Money coordinatorFee) => OutputSumWithoutCoordinatorFeeAndDenomination - denomination - coordinatorFee;

		public Alice(Dictionary<OutPoint, TxOut> inputs, Money feeToPay, Script changeOutputScript)
		{
			Guard.NotNullOrEmpty(nameof(inputs), inputs);
			Inputs = inputs;
			FeeToPay = Guard.NotNull(nameof(feeToPay), feeToPay);

			Guard.NotNull(nameof(changeOutputScript), changeOutputScript);
			// 33 bytes maximum: https://bitcoin.stackexchange.com/a/46379/26859
			int byteCount = changeOutputScript.ToBytes().Length;
			if (byteCount > 33)
			{
				throw new ArgumentOutOfRangeException(nameof(changeOutputScript), byteCount, $"Can be maximum 33 bytes.");
			}
			ChangeOutputScript = changeOutputScript;

			LastSeen = DateTimeOffset.UtcNow;

			UniqueId = Guid.NewGuid();

			InputSum = inputs.Sum(x => x.Value.Value);

			OutputSumWithoutCoordinatorFeeAndDenomination = InputSum - FeeToPay;
		}
    }
}
