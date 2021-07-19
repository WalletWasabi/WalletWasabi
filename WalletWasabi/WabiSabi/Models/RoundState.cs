using System;
using NBitcoin;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Models
{
	public record RoundState(
		uint256 Id,
		uint256? BlameOf,
		CredentialIssuerParameters AmountCredentialIssuerParameters,
		CredentialIssuerParameters VsizeCredentialIssuerParameters,
		FeeRate FeeRate,
		Phase Phase,
		bool WasTransactionBroadcast,
		TimeSpan ConnectionConfirmationTimeout,
		long MaxRegistrableAmount,
		long MaxRegistrableVsize,
		long MaxVsizeAllocationPerAlice,
		MultipartyTransactionState CoinjoinState)
	{
		public static RoundState FromRound(Round round) =>
			new(
				round.Id,
				round.BlameOf?.Id,
				round.AmountCredentialIssuerParameters,
				round.VsizeCredentialIssuerParameters,
				round.FeeRate,
				round.Phase,
				round.WasTransactionBroadcast,
				round.ConnectionConfirmationTimeout,
				round.MaxRegistrableAmount,
				round.MaxRegistrableVsize,
				round.MaxVsizeAllocationPerAlice,
				round.CoinjoinState);

		public TState Assert<TState>() where TState : MultipartyTransactionState =>
			CoinjoinState switch
			{
				TState s => s,
				_ => throw new InvalidOperationException($"{typeof(TState).Name} state was expected but {CoinjoinState.GetType().Name} state was received.")
			};

		public WabiSabiClient CreateAmountCredentialClient(WasabiRandom random) =>
			new(AmountCredentialIssuerParameters, random, MaxRegistrableAmount);

		public WabiSabiClient CreateVsizeCredentialClient(WasabiRandom random) =>
			new(VsizeCredentialIssuerParameters, random, MaxRegistrableVsize);
	}
}
