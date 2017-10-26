using HiddenWallet.ChaumianTumbler.Models;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler
{
    public sealed class TumblerPhaseBroadcaster
    {
		private readonly static Lazy<TumblerPhaseBroadcaster> _instance = new Lazy<TumblerPhaseBroadcaster>(() => new TumblerPhaseBroadcaster());

		private IHubContext<TumblerHub> _context; //The context of the hub - needed in order for the tumbler to act on MVC submitted data and call the hub to issue updates

		private TumblerPhaseBroadcaster()
 		{
 			//	Put any code to initliase collections etc. here.
 		}

		public static TumblerPhaseBroadcaster Instance => _instance.Value;

		public IHubContext<TumblerHub> SignalRHub
		{
			set
			{
				_context = value;
			}
		}

		public void Broadcast(PhaseChangeBroadcast broadcast)
		{
			IClientProxy proxy = _context.Clients.All;
			string json = JsonConvert.SerializeObject(broadcast);
			proxy.InvokeAsync("phaseChange", json);
		}
	}
}
