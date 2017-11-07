using HiddenWallet.FullSpv;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace HiddenWallet.FullSpvWallet.ChaumianCoinJoin
{
    public class CoinJoinService
	{
		public HubConnection TumblerConnection { get; private set; }
		public WalletJob WalletJob { get; private set; }

		public CoinJoinService(WalletJob walletJob)
		{
			TumblerConnection = null;
			WalletJob = walletJob ?? throw new ArgumentNullException(nameof(walletJob));
		}

		public async Task SubscribePhaseChangeAsync(string address)
		{
			try
			{
				TumblerConnection = new HubConnectionBuilder()
						.WithUrl(address)
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
			}
			catch (Exception)
			{

			}
		}

		#endregion
	}
}
