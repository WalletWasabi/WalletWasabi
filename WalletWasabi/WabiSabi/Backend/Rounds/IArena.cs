using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Rounds
{
	public interface IArena
	{
		public bool TryGetRound(Guid roundId, [NotNullWhen(true)] out Round? round);
	}
}
