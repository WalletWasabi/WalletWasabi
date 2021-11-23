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

namespace WalletWasabi.EventSourcing.ArenaDomain.Aggregates
{
	public record RoundState2(RoundParameters RoundParameters) : IState
	{
		public ImmutableList<InputState> Inputs { get; init; } = ImmutableList<InputState>.Empty;
		public ImmutableList<OutputState> Outputs { get; init; } = ImmutableList<OutputState>.Empty;
		public Phase Phase { get; init; } = Phase.InputRegistration;
	}

	public record InputState(
		Guid AliceId,
		Coin Coin,
		OwnershipProof OwnershipProof,
		bool ConnectionConfirmed = false,
		bool ReadyToSign = false,
		WitScript? WitScript = null);

	public record OutputState(Script Script, long CredentialAmount);
}
