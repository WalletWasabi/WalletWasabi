using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.Events;

internal class CoinJoinStatusEventArgs : StatusChangedEventArgs
{
	public CoinJoinStatusEventArgs(Wallet wallet, CoinJoinProgressEventArgs coinJoinProgressEventArgs) : base(wallet)
	{
		CoinJoinProgressEventArgs = coinJoinProgressEventArgs;
	}

	public CoinJoinProgressEventArgs CoinJoinProgressEventArgs { get; }
}
