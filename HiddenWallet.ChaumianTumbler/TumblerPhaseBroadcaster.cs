using HiddenWallet.ChaumianTumbler.Models;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler
{
    public sealed class TumblerPhaseBroadcaster
    {
		private readonly static Lazy<TumblerPhaseBroadcaster> _instance = new Lazy<TumblerPhaseBroadcaster>(() => new TumblerPhaseBroadcaster());

		private readonly ConcurrentDictionary<string, string> _connectedClients = new ConcurrentDictionary<string, string>();

		private IHubContext<TumblerHub> _context;
		
		private TumblerPhaseBroadcaster()
 		{ }

		public static TumblerPhaseBroadcaster Instance => _instance.Value;

		public IHubContext<TumblerHub> SignalRHub
		{
			set
			{
				_context = value;
			}
		}

		public void AddConnectedClient(string id) => _connectedClients.TryAdd(id, id);

		public void RemoveConnectedClient(string id) => _connectedClients.Remove(id, out string value);

		public int ConnectedClientCount() => _connectedClients.Count();

		public void Broadcast(PhaseChangeBroadcast broadcast)
		{
			IClientProxy proxy = _context.Clients.All;
			string json = JsonConvert.SerializeObject(broadcast);
			proxy.InvokeAsync("PhaseChange", json);
		}
	}
}
