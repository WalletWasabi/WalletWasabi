using HiddenWallet.ChaumianCoinJoin.Models;
using HiddenWallet.Models;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon
{
	public sealed class NotificationBroadcaster
	{
		private readonly static Lazy<NotificationBroadcaster> _instance = new Lazy<NotificationBroadcaster>(() => new NotificationBroadcaster());

		private IHubContext<DaemonHub> _context;

		private NotificationBroadcaster() { }

		public static NotificationBroadcaster Instance => _instance.Value;

		public IHubContext<DaemonHub> SignalRHub
		{
			set
			{
				_context = value;
			}
		}

		public void BroadcastMempool(string mempoolCount)
		{
			IClientProxy proxy = _context.Clients.All;
			proxy.InvokeAsync("mempoolChanged", mempoolCount);
		}

		public void BroadcastTrackerHeight(string trackerHeight)
		{
			IClientProxy proxy = _context.Clients.All;
			proxy.InvokeAsync("trackerHeightChanged", trackerHeight);
		}

		public void BroadcastHeaderHeight(string headerHeight)
		{
			IClientProxy proxy = _context.Clients.All;
			proxy.InvokeAsync("headerHeightChanged", headerHeight);
		}

		public void BroadcastNodeCount(string nodeCount)
		{
			IClientProxy proxy = _context.Clients.All;
			proxy.InvokeAsync("nodeCountChanged", nodeCount);
		}

		public void BroadcastWalletState(string state)
		{
			IClientProxy proxy = _context.Clients.All;
			proxy.InvokeAsync("walletStateChanged", state);
		}

		public void BroadcastChangeBump()
		{
			IClientProxy proxy = _context.Clients.All;
			proxy.InvokeAsync("changeBump");
		}

		public void BroadcastTorState(string state)
		{
			if(_context != null)
			{
				IClientProxy proxy = _context.Clients.All;
				proxy.InvokeAsync("torStateChanged", state);
			}
			else
			{
				Console.WriteLine("IHubContext not set yet. Tor Status Broadcast failed.");
			}
		}
	}
}
