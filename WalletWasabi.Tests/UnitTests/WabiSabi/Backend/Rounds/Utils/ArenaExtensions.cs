using System.Collections.Generic;
using System.Linq;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.Rounds.Utils;

public static class ArenaExtensions
{
	public static IEnumerable<Round> GetActiveRounds(this Arena arena)
		=> arena.Rounds.Where(x => x.Phase != Phase.Ended);
}
