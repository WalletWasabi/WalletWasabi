using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Rounds
{
	public class Arena : IArena
	{
		public bool TryGetRound(Guid roundId, [NotNullWhen(true)] out Round? round)
		{
			throw new NotImplementedException();
		}
	}
}
