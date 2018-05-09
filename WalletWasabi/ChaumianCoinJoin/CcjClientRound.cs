using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.ChaumianCoinJoin
{
	public class CcjClientRound
	{
		public CcjRunningRoundState State { get; set; }

		public Guid? AliceUniqueId { get; set; }

		public List<MixCoin> CoinsRegistered { get; }

		public CcjClientRound(CcjRunningRoundState state)
		{
			State = Guard.NotNull(nameof(state), state);
			CoinsRegistered = new List<MixCoin>();
			AliceUniqueId = null;
		}

		public void ClearRegistration()
		{
			AliceUniqueId = null;
			CoinsRegistered.Clear();
		}
	}
}
