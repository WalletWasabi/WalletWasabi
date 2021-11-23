using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.EventSourcing.ArenaDomain.Aggregates
{
	public record RoundState2(RoundParameters RoundParameters)
	{
		public ImmutableList<InputState> Inputs { get; init; } = ImmutableList<InputState>.Empty;
		public Phase Phase { get; init; } = Phase.InputRegistration;
	}

	public record InputState(Coin Coin, OwnershipProof OwnershipProof, Guid AliceId, bool ConnectionConfirmed = false);
}
