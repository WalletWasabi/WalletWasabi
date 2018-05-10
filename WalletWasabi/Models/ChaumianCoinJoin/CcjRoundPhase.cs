using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	public enum CcjRoundPhase
	{
		InputRegistration,
		ConnectionConfirmation,
		OutputRegistration,
		Signing,
	}
}
