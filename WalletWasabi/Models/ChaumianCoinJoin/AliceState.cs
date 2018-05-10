using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	public enum AliceState
	{
		InputsRegistered,
		ConnectionConfirmed,
		SignedCoinJoin
	}
}
