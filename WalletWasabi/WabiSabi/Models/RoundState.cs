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
		uint256? BlameOf,
		CredentialIssuerParameters AmountCredentialIssuerParameters,
		CredentialIssuerParameters VsizeCredentialIssuerParameters,
		FeeRate FeeRate,
		Phase Phase,
		bool WasTransactionBroadcast,
		TimeSpan InputRegistrationTimeout,
		TimeSpan ConnectionConfirmationTimeout,
		TimeSpan OutputRegistrationTimeout,
		TimeSpan TransactionSigningTimeout,
		long MinRegistrableAmount,
		long MaxRegistrableAmount,
		long MaxRegistrableVsize,
		long MaxVsizeAllocationPerAlice,
		MultipartyTransactionState CoinjoinState)
	{
		private uint256 _id;

		public uint256 Id => _id ??= CalculateHash();

		public static RoundState FromRound(Round round) =>
			new(
				round.BlameOf?.Id,
				round.AmountCredentialIssuerParameters,
				round.VsizeCredentialIssuerParameters,
				round.FeeRate,
				round.Phase,
				round.WasTransactionBroadcast,
				round.InputRegistrationTimeout,
				round.ConnectionConfirmationTimeout,
				round.OutputRegistrationTimeout,
				round.TransactionSigningTimeout,
				round.MinRegistrableAmount,
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

		private uint256 CalculateHash() =>
			RoundHasher.CalculateHash(
				InputRegistrationTimeout,
				ConnectionConfirmationTimeout,
				OutputRegistrationTimeout,
				TransactionSigningTimeout,
				CoinjoinState.Parameters.AllowedInputAmounts,
				CoinjoinState.Parameters.AllowedInputTypes,
				CoinjoinState.Parameters.AllowedOutputAmounts,
				CoinjoinState.Parameters.AllowedOutputTypes,
				CoinjoinState.Parameters.Network,
				CoinjoinState.Parameters.FeeRate.FeePerK,
				CoinjoinState.Parameters.MaxTransactionSize,
				CoinjoinState.Parameters.MinRelayTxFee.FeePerK,
				MaxRegistrableVsize,
				MaxVsizeAllocationPerAlice,
				AmountCredentialIssuerParameters,
				VsizeCredentialIssuerParameters);
	}
}
