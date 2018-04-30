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

		public Money NetworkFeeToPay { get; }

		public Money OutputSumWithoutCoordinatorFeeAndDenomination { get; }

		public IEnumerable<(OutPoint OutPoint, TxOut Output)> Inputs { get; }

		public Script ChangeOutputScript { get; }

		public string BlindedOutputScriptHex { get; }

		public Money GetChangeAmount(Money denomination, Money coordinatorFee) => OutputSumWithoutCoordinatorFeeAndDenomination - denomination - coordinatorFee;

		public AliceState State { get; set; }

		public Alice(IEnumerable<(OutPoint OutPoint, TxOut Output)> inputs, Money networkFeeToPay, Script changeOutputScript, string blindedOutputHex)
		{
			Inputs = Guard.NotNullOrEmpty(nameof(inputs), inputs);
			NetworkFeeToPay = Guard.NotNull(nameof(networkFeeToPay), networkFeeToPay);
			BlindedOutputScriptHex = Guard.NotNullOrEmptyOrWhitespace(nameof(blindedOutputHex), blindedOutputHex);

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

			InputSum = inputs.Sum(x => x.Output.Value);

			OutputSumWithoutCoordinatorFeeAndDenomination = InputSum - NetworkFeeToPay;

			State = AliceState.InputsRegistered;
		}
    }
}
