using NBitcoin;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Coordinator.WabiSabi;

public static class RoundParametersExtensions
{
	extension(RoundParameters)
	{
		public static RoundParameters Create(
			WabiSabiConfig wabiSabiConfig,
			FeeRate miningFeeRate,
			Money maxSuggestedAmount)
		{
			return new RoundParameters(
				wabiSabiConfig.Network,
				miningFeeRate,
				maxSuggestedAmount,
				wabiSabiConfig.MinInputCountByRound,
				wabiSabiConfig.MaxInputCountByRound,
				new MoneyRange(wabiSabiConfig.MinRegistrableAmount, wabiSabiConfig.MaxRegistrableAmount),
				new MoneyRange(wabiSabiConfig.MinRegistrableAmount, wabiSabiConfig.MaxRegistrableAmount),
				wabiSabiConfig.AllowedInputTypes,
				wabiSabiConfig.AllowedOutputTypes,
				wabiSabiConfig.StandardInputRegistrationTimeout,
				wabiSabiConfig.ConnectionConfirmationTimeout,
				wabiSabiConfig.OutputRegistrationTimeout,
				wabiSabiConfig.TransactionSigningTimeout,
				wabiSabiConfig.BlameInputRegistrationTimeout,
				wabiSabiConfig.CoordinatorIdentifier,
				wabiSabiConfig.DelayTransactionSigning);
		}
	}
}
