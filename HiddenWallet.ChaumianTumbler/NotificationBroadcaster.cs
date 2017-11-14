using HiddenWallet.ChaumianCoinJoin.Models;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler
{
    public sealed class NotificationBroadcaster
    {
		private readonly static Lazy<NotificationBroadcaster> _instance = new Lazy<NotificationBroadcaster>(() => new NotificationBroadcaster());

		private readonly ConcurrentDictionary<string, string> _connectedClients = new ConcurrentDictionary<string, string>();

		private IHubContext<TumblerHub> _context;
		
		private NotificationBroadcaster()
 		{ }

		public static NotificationBroadcaster Instance => _instance.Value;

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

		public async Task BroadcastAsync(PhaseChangeBroadcast broadcast)
		{
			while (_context == null)
			{
				await Task.Delay(100);
			}
			IClientProxy proxy = _context.Clients.All;
			string json = JsonConvert.SerializeObject(broadcast);
			await proxy.InvokeAsync("PhaseChange", json);
		}

		public async Task BroadcastAsync(PeerRegisteredBroadcast broadcast)
		{
			while (_context == null)
			{
				await Task.Delay(100);
			}
			IClientProxy proxy = _context.Clients.All;
			string json = JsonConvert.SerializeObject(broadcast);
			await proxy.InvokeAsync("PeerRegistered", json);
		}
	}
}
