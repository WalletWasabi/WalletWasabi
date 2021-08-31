using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Client
{
	public static class CoinJoinClientStateExtensions
	{
		public static bool IsInCriticalSection(this CoinJoinClientState state)
		{
			return state >= CoinJoinClientState.ConnectionConfirmed;
		}

		public static bool IsCoinJoinInProgress(this CoinJoinClientState state)
		{
			return state > CoinJoinClientState.Idle;
		}
	}
}
