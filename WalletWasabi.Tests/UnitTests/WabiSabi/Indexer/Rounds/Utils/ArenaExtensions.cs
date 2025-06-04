using System.Linq;
using System.Collections.Generic;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using Arena = WalletWasabi.WabiSabi.Coordinator.Rounds.Arena;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Indexer.Rounds.Utils;

public static class ArenaExtensions
{
	public static IEnumerable<Round> GetActiveRounds(this Arena arena)
		=> arena.Rounds.Where(x => x.Phase != Phase.Ended);
}
