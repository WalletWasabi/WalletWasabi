using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Models.EventSourcing
{
	public record ActiveRoundsState
	{
		public ImmutableList<Round> Rounds { get; init; } = ImmutableList<Round>.Empty;
		public IEnumerable<Round> InPhase(Phase phase) => Rounds.Where(x => x.Phase == phase);
	}
}
