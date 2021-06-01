using System;
using NBitcoin;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Models
{
	public record RoundState(
		uint256 Id,
		CredentialIssuerParameters AmountCredentialIssuerParameters,
		CredentialIssuerParameters VsizeCredentialIssuerParameters,
		FeeRate FeeRate,
		Phase Phase,
		TimeSpan ConnectionConfirmationTimeout,
		MultipartyTransactionState CoinjoinState)
	{
		public static RoundState FromRound(Round round) =>
			new RoundState(round.Id, round.AmountCredentialIssuerParameters, round.VsizeCredentialIssuerParameters, round.FeeRate, round.Phase, round.ConnectionConfirmationTimeout, round.CoinjoinState);

		public TState Assert<TState>() where TState : MultipartyTransactionState =>
			CoinjoinState switch
			{
				TState s => s,
				_ => throw new InvalidOperationException($"{typeof(TState).Name} state was expected but {CoinjoinState.GetType().Name} state was received.")
			};
	}
}
