using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Wallets
{
	public enum WalletState
	{
		Uninitialized,
		WaitingForInit,
		Initialized,
		Starting,
		Started,
		Stopping,
		Stopped
	}
}
