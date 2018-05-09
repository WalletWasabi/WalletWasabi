using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Helpers;

namespace WalletWasabi.ChaumianCoinJoin
{
    public class CcjClientRound
    {
		public CcjRunningRoundState State { get; set; }

		public Guid? AliceUniqueId { get; set; }

		public CcjClientRound(CcjRunningRoundState state)
		{
			State = Guard.NotNull(nameof(state), state);
			AliceUniqueId = null;
		}
    }
}
