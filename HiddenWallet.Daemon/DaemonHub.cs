using HiddenWallet.Daemon.Models;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon
{
	public class DaemonHub : Hub
	{
		public async Task GetTorStatusAsync()
		{
			string status = Tor.State.ToString();
			await NotificationBroadcaster.Instance.BroadcastTorStateAsync(status);
		}

		public async Task GetHeaderHeightAsync()
		{
			var (Success, Height) = await Global.WalletWrapper.WalletJob.TryGetHeaderHeightAsync();
			await NotificationBroadcaster.Instance.BroadcastHeaderHeightAsync(Height.Value.ToString());
		}

		public async Task TumblerStatusBroadcastRequestAsync()
		{
			TumblerStatusResponse status = Global.WalletWrapper.GetTumblerStatusResponse();
			await NotificationBroadcaster.Instance.BroadcastTumblerStatusAsync(status);
		}
	}
}