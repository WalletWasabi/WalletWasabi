using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Models;

public record ArenaRoundState(
	uint256 BlameOf,
	CredentialIssuerParameters AmountCredentialIssuerParameters,
	CredentialIssuerParameters VsizeCredentialIssuerParameters,
	FeeRate FeeRate,
	CoordinationFeeRate CoordinationFeeRate,
	Phase Phase,
	bool WasTransactionBroadcast,
	DateTimeOffset InputRegistrationStart,
	TimeSpan InputRegistrationTimeout,
	TimeSpan ConnectionConfirmationTimeout,
	TimeSpan OutputRegistrationTimeout,
	TimeSpan TransactionSigningTimeout,
	long MaxAmountCredentialValue,
	long MaxVsizeCredentialValue,
	long MaxVsizeAllocationPerAlice,
	long MaxSuggestedAmount,
	MultipartyTransactionState CoinjoinState,
	DateTimeOffset End)
	: RoundState(
		BlameOf,
		AmountCredentialIssuerParameters,
		VsizeCredentialIssuerParameters,
		FeeRate,
		CoordinationFeeRate,
		Phase,
		WasTransactionBroadcast,
		InputRegistrationStart,
		InputRegistrationTimeout,
		ConnectionConfirmationTimeout,
		OutputRegistrationTimeout,
		TransactionSigningTimeout,
		MaxAmountCredentialValue,
		MaxVsizeCredentialValue,
		MaxVsizeAllocationPerAlice,
		MaxSuggestedAmount,
		CoinjoinState)
{
	public TimeFrame InputRegistrationTimeFrame => TimeFrame.Create(InputRegistrationStart, InputRegistrationTimeout);
	public static ArenaRoundState FromRound(Round round) =>
		new(
			round is BlameRound blameRound ? blameRound.BlameOf.Id : uint256.Zero,
			round.AmountCredentialIssuerParameters,
			round.VsizeCredentialIssuerParameters,
			round.FeeRate,
			round.CoordinationFeeRate,
			round.Phase,
			round.WasTransactionBroadcast,
			round.InputRegistrationTimeFrame.StartTime,
			round.InputRegistrationTimeFrame.Duration,
			round.ConnectionConfirmationTimeFrame.Duration,
			round.OutputRegistrationTimeFrame.Duration,
			round.TransactionSigningTimeFrame.Duration,
			round.MaxAmountCredentialValue,
			round.MaxVsizeCredentialValue,
			round.MaxVsizeAllocationPerAlice,
			round.MaxSuggestedAmount,
			round.CoinjoinState.GetStateFrom(0),
			round.End
			);

	public bool IsInputRegistrationEnded(int maxInputCount)
	{
		if (Phase > Phase.InputRegistration)
		{
			return true;
		}

		if (CoinjoinState.Inputs.Count() >= maxInputCount)
		{
			return true;
		}

		return InputRegistrationTimeFrame.HasExpired;
	}
}
