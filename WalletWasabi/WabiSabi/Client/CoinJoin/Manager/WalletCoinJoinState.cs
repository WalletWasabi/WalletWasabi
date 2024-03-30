using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Manager;

public class WalletCoinJoinState
{
	public WalletCoinJoinState(IWallet wallet)
	{
		Wallet = wallet;
	}

	public IWallet Wallet { get; }

	public bool IsCoinJoining { get; set; }

	public bool IsOverridePlebStop { get; set; }

	public bool IsStartTriggered { get; set; }
	public bool IsStopTriggered { get; set; }

	public bool StopWhenAllMixed { get; set; }
}
