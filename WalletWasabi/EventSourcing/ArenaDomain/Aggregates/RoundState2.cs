using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Crypto;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.EventSourcing.ArenaDomain.Aggregates
{
	public record RoundState2() : IState
	{
		public RoundParameters2? RoundParameters { get; init; } = default;
		public ImmutableList<InputState> Inputs { get; init; } = ImmutableList<InputState>.Empty;
		public ImmutableList<OutputState> Outputs { get; init; } = ImmutableList<OutputState>.Empty;
		public Phase Phase { get; init; } = Phase.New;
		public uint256 Id => RoundParameters?.Id ?? uint256.Zero;
		public bool Succeeded { get; init; } = false;
	}

	public record InputState(
		Coin Coin,
		OwnershipProof OwnershipProof,
		Guid AliceSecret = default,
		bool ConnectionConfirmed = false,
		bool ReadyToSign = false,
		WitScript? WitScript = null);

	public record OutputState(
		Script Script,
		long Value);

	public record RoundParameters2(
		FeeRate FeeRate,
		CredentialIssuerParameters AmountCredentialIssuerParameters,
		CredentialIssuerParameters VsizeCredentialIssuerParameters,
		DateTimeOffset InputRegistrationStart,
		TimeSpan InputRegistrationTimeout,
		TimeSpan ConnectionConfirmationTimeout,
		TimeSpan OutputRegistrationTimeout,
		TimeSpan TransactionSigningTimeout,
		long MaxAmountCredentialValue,
		long MaxVsizeCredentialValue,
		long MaxVsizeAllocationPerAlice,
		MultipartyTransactionParameters MultipartyTransactionParameters
	)
	{
		public uint256 BlameOf { get; init; } = uint256.Zero;

		private uint256? _id;
		public uint256 Id => _id ??= CalculateHash();
		public DateTimeOffset InputRegistrationEnd => InputRegistrationStart + InputRegistrationTimeout;

		private uint256 CalculateHash() =>
			RoundHasher.CalculateHash(
				InputRegistrationStart,
				InputRegistrationTimeout,
				ConnectionConfirmationTimeout,
				OutputRegistrationTimeout,
				TransactionSigningTimeout,
				MultipartyTransactionParameters.AllowedInputAmounts,
				MultipartyTransactionParameters.AllowedInputTypes,
				MultipartyTransactionParameters.AllowedOutputAmounts,
				MultipartyTransactionParameters.AllowedOutputTypes,
				MultipartyTransactionParameters.Network,
				MultipartyTransactionParameters.FeeRate.FeePerK,
				MultipartyTransactionParameters.MaxTransactionSize,
				MultipartyTransactionParameters.MinRelayTxFee.FeePerK,
				MaxAmountCredentialValue,
				MaxVsizeCredentialValue,
				MaxVsizeAllocationPerAlice,
				AmountCredentialIssuerParameters,
				VsizeCredentialIssuerParameters);
	}
}
