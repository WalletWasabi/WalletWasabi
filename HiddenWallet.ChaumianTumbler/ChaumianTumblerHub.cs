using HiddenWallet.ChaumianTumbler.Models;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler
{
	public class ChaumianTumblerHub : Hub
	{
		//public async Task SendStatusResponseAsync(PhaseChangeBroadcast broadcast)
		//{
		//	await Clients.All.InvokeAsync("phaseChange", broadcast);
		//}
	}
}
