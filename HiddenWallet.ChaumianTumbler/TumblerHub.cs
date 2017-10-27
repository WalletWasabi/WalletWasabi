using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler
{
	public class TumblerHub : Hub
	{
		public override async Task OnConnectedAsync()
		{
			TumblerPhaseBroadcaster.Instance.AddConnectedClient(Context.ConnectionId);
			await base.OnConnectedAsync();
		}

		public override async Task OnDisconnectedAsync(Exception exception)
		{
			TumblerPhaseBroadcaster.Instance.RemoveConnectedClient(Context.ConnectionId);
			await base.OnDisconnectedAsync(exception);
		}
	}
}