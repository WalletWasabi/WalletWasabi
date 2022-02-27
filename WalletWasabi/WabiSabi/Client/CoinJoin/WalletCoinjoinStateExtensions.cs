using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Client.CoinJoin;

public static class WalletCoinjoinStateExtensions
{
	public static string ToFriendlyString(this WalletCoinjoinState.State me)
	{
		return me switch
		{
			WalletCoinjoinState.State.AutoStarting => "Auto Starting",
			WalletCoinjoinState.State.Playing => "Playing",
			WalletCoinjoinState.State.Paused => "Paused",
			WalletCoinjoinState.State.Finished => "Finished",
			WalletCoinjoinState.State.LoadingTrack => "Loading Track",
			WalletCoinjoinState.State.Stopped => "Stopped",
			_ => "Missing implementation.",
		};
	}
}
