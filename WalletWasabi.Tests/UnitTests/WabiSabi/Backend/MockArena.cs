using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend
{
	public class MockArena : IArena
	{
		public Func<Guid, Round?> OnTryGetRound { get; set; } = (roundId) => null;

		public bool TryGetRound(Guid roundId, [NotNullWhen(true)] out Round? round)
		{
			round = OnTryGetRound(roundId);
			return round is not null;
		}
	}
}
