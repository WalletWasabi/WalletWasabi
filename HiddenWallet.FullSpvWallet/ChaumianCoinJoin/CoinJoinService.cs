using HiddenWallet.ChaumianCoinJoin;
using HiddenWallet.FullSpv;
using HiddenWallet.KeyManagement;
using Microsoft.AspNetCore.SignalR.Client;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace HiddenWallet.FullSpvWallet.ChaumianCoinJoin
{
	public class CoinJoinService
	{
		public HubConnection TumblerConnection { get; private set; }
		public WalletJob WalletJob { get; private set; }
		public ChaumianTumblerClient TumblerClient { get; private set; }
		public string BaseAddress { get; private set; }

		public CoinJoinService(WalletJob walletJob)
		{
			TumblerConnection = null;
			WalletJob = walletJob ?? throw new ArgumentNullException(nameof(walletJob));
		}

		public void SetConnection(string address, HttpMessageHandler handler, bool disposeHandler = false)
		{
			BaseAddress = address.EndsWith('/') ? address : address + "/";
			TumblerClient = new ChaumianTumblerClient(BaseAddress, handler, disposeHandler);
		}

		#region Notifications

		public async Task SubscribePhaseChangeAsync()
		{
			try
			{
				TumblerConnection = new HubConnectionBuilder()
						.WithUrl(BaseAddress + "ChaumianTumbler")
						.Build();

				TumblerConnection.On<string>("PhaseChange", data =>
				{
					Debug.WriteLine($"Received: {data}");
				});

				await TumblerConnection.StartAsync();

				TumblerConnection.Closed += TumblerConnection_ClosedAsync;
			}
			catch (Exception ex)
			{
				await DisposeAsync();
				Debug.WriteLine(ex);
			}
		}

		private async Task TumblerConnection_ClosedAsync(Exception arg)
		{
			await DisposeAsync();
		}

		#endregion
		
		#region Tumbling

		/// <summary>
		/// Participates one tumbling round
		/// </summary>
		public async Task TumbleAsync(SafeAccount from, SafeAccount to)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (to == null) throw new ArgumentNullException(nameof(to));
			
			// if blocks are not synced yet throw
			if(WalletJob.State < WalletState.SyncingMemPool)
			{
				throw new InvalidOperationException("Blocks are not synced");
			}

			throw new NotImplementedException();
		}

		#endregion

		#region Disposing

		public async Task DisposeAsync()
		{
			try
			{
				if (TumblerConnection != null)
				{
					try
					{
						TumblerConnection.Closed -= TumblerConnection_ClosedAsync;
					}
					catch (Exception)
					{

					}
					await TumblerConnection?.DisposeAsync();
					TumblerConnection = null;
				}
				TumblerClient?.Dispose();
			}
			catch (Exception)
			{

			}
		}

		#endregion
	}
}
