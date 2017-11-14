using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler
{
	public class TumblerHub : Hub
	{
		public override async Task OnConnectedAsync()
		{
			NotificationBroadcaster.Instance.AddConnectedClient(Context.ConnectionId);
			await base.OnConnectedAsync();
		}

		public override async Task OnDisconnectedAsync(Exception exception)
		{
			NotificationBroadcaster.Instance.RemoveConnectedClient(Context.ConnectionId);
			await base.OnDisconnectedAsync(exception);
		}
	}
}