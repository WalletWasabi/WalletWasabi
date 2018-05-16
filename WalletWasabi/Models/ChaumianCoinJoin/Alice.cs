using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	public class Alice
	{
		public DateTimeOffset LastSeen { get; set; }

		public Guid UniqueId { get; }

		public Money InputSum { get; }

		public Money NetworkFeeToPay { get; }

		public Money OutputSumWithoutCoordinatorFeeAndDenomination { get; }

		public IEnumerable<(OutPoint OutPoint, TxOut Output)> Inputs { get; }

		public BitcoinAddress ChangeOutputAddress { get; }

		public string BlindedOutputScriptHex { get; }

		public Money GetChangeAmount(Money denomination, Money coordinatorFee) => OutputSumWithoutCoordinatorFeeAndDenomination - denomination - coordinatorFee;

		public AliceState State { get; set; }

		public Alice(IEnumerable<(OutPoint OutPoint, TxOut Output)> inputs, Money networkFeeToPay, BitcoinAddress changeOutputAddress, string blindedOutputScriptHex)
		{
			Inputs = Guard.NotNullOrEmpty(nameof(inputs), inputs);
			NetworkFeeToPay = Guard.NotNull(nameof(networkFeeToPay), networkFeeToPay);
			BlindedOutputScriptHex = Guard.NotNullOrEmptyOrWhitespace(nameof(blindedOutputScriptHex), blindedOutputScriptHex);

			Guard.NotNull(nameof(changeOutputAddress), changeOutputAddress);
			// 33 bytes maximum: https://bitcoin.stackexchange.com/a/46379/26859
			int byteCount = changeOutputAddress.ScriptPubKey.ToBytes().Length;
			if (byteCount > 33)
			{
				throw new ArgumentOutOfRangeException(nameof(changeOutputAddress), byteCount, $"Can be maximum 33 bytes.");
			}
			ChangeOutputAddress = changeOutputAddress;
			LastSeen = DateTimeOffset.UtcNow;

			UniqueId = Guid.NewGuid();

			InputSum = inputs.Sum(x => x.Output.Value);

			OutputSumWithoutCoordinatorFeeAndDenomination = InputSum - NetworkFeeToPay;

			State = AliceState.InputsRegistered;
		}
	}
}
