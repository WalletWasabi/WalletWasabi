using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Models.EventSourcing
{
	public record RoundsState
	{
		public ImmutableList<Round> Rounds { get; init; } = ImmutableList<Round>.Empty;

		public IEnumerable<Round> InInputRegistration =>  Rounds.Where(x => x.Phase == Phase.InputRegistration);
		public IEnumerable<Round> InConnectionConfirmation =>  Rounds.Where(x => x.Phase == Phase.ConnectionConfirmation);
		public IEnumerable<Round> InOutputRegistration =>  Rounds.Where(x => x.Phase == Phase.OutputRegistration);
		public IEnumerable<Round> InTransactionSigning =>  Rounds.Where(x => x.Phase == Phase.TransactionSigning);
	}
}
