using HiddenWallet.Daemon.Models;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon
{
	public class DaemonHub : Hub
	{
		public void GetTorStatus()
		{
			string status = Tor.State.ToString();
			NotificationBroadcaster.Instance.BroadcastTorState(status);
		}

		public async Task GetHeaderHeightAsync()
		{
			int hh = await Global.WalletWrapper.GetHeaderHeightAsync();
			NotificationBroadcaster.Instance.BroadcastHeaderHeight(hh.ToString());
		}

		public void TumblerStatusBroadcastRequest()
		{
			TumblerStatusResponse status = Global.WalletWrapper.GetTumblerStatusResponse();
			NotificationBroadcaster.Instance.BroadcastTumblerStatus(status);
		}
	}
}