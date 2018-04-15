using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.ChaumianCoinJoin
{
	public enum CcjRoundPhase
	{
		InputRegistration,
		ConnectionConfirmation,
		OutputRegistration,
		Signing,
	}
}
