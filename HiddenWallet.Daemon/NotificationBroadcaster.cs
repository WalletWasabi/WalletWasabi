using HiddenWallet.ChaumianCoinJoin.Models;
using HiddenWallet.Daemon.Models;
using HiddenWallet.Models;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Nito.AsyncEx;
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

		private static AsyncLock AsyncLock = new AsyncLock();

		private NotificationBroadcaster() { }

		public static NotificationBroadcaster Instance => _instance.Value;

		public IHubContext<DaemonHub> SignalRHub
		{
			set
			{
				_context = value;
			}
		}

		public async Task BroadcastMempoolAsync(string mempoolCount)
		{
			using (await AsyncLock.LockAsync())
			{
				await DelayUntilContextNullAsync();
				IClientProxy proxy = _context.Clients.All;
				await proxy.InvokeAsync("mempoolChanged", mempoolCount);
			}
		}

		public async Task BroadcastTrackerHeightAsync(string trackerHeight)
		{
			using (await AsyncLock.LockAsync())
			{
				await DelayUntilContextNullAsync();
				IClientProxy proxy = _context.Clients.All;
				await proxy.InvokeAsync("trackerHeightChanged", trackerHeight);
			}
		}

		public async Task BroadcastHeaderHeightAsync(string headerHeight)
		{
			using (await AsyncLock.LockAsync())
			{
				await DelayUntilContextNullAsync();
				IClientProxy proxy = _context.Clients.All;
				await proxy.InvokeAsync("headerHeightChanged", headerHeight);
			}
		}

		public async Task BroadcastNodeCountAsync(string nodeCount)
		{
			using (await AsyncLock.LockAsync())
			{
				await DelayUntilContextNullAsync();
				IClientProxy proxy = _context.Clients.All;
				await proxy.InvokeAsync("nodeCountChanged", nodeCount);
			}
		}

		public async Task BroadcastWalletStateAsync(string state)
		{
			using (await AsyncLock.LockAsync())
			{
				await DelayUntilContextNullAsync();
				IClientProxy proxy = _context.Clients.All;
				await proxy.InvokeAsync("walletStateChanged", state);
			}
		}

		public async Task BroadcastChangeBumpAsync()
		{
			using (await AsyncLock.LockAsync())
			{
				await DelayUntilContextNullAsync();
				IClientProxy proxy = _context.Clients.All;
				await proxy.InvokeAsync("changeBump");
			}
		}

		public async Task BroadcastTorStateAsync(string state)
		{
			using (await AsyncLock.LockAsync())
			{
				await DelayUntilContextNullAsync();
				IClientProxy proxy = _context.Clients.All;
				await proxy.InvokeAsync("torStateChanged", state);
			}
		}

		public async Task BroadcastTumblerStatusAsync(TumblerStatusResponse status)
		{
			using (await AsyncLock.LockAsync())
			{
				await DelayUntilContextNullAsync();
				IClientProxy proxy = _context.Clients.All;
				string json = JsonConvert.SerializeObject(status);
				await proxy.InvokeAsync("mixerStatusChanged", json);
			}
		}

		private async Task DelayUntilContextNullAsync()
		{
			while (_context == null)
			{
				await Task.Delay(100);
			}
		}
	}
}