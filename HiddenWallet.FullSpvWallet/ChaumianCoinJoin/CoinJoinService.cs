using HiddenWallet.ChaumianCoinJoin;
using HiddenWallet.ChaumianCoinJoin.Models;
using HiddenWallet.FullSpv;
using HiddenWallet.KeyManagement;
using HiddenWallet.Models;
using Microsoft.AspNetCore.SignalR.Client;
using NBitcoin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static HiddenWallet.FullSpv.WalletJob;

namespace HiddenWallet.FullSpvWallet.ChaumianCoinJoin
{
	public class CoinJoinService
	{
		public HubConnection TumblerConnection { get; private set; }
		public WalletJob WalletJob { get; private set; }
		public ChaumianTumblerClient TumblerClient { get; private set; }
		public string BaseAddress { get; private set; }

		public SafeAccount From { get; private set; }
		public SafeAccount To { get; private set; }

		private volatile bool _tumblingInProcess;

		public CoinJoinService(WalletJob walletJob)
		{
			_tumblingInProcess = false;
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

				TumblerConnection.On<string>("PhaseChange", async data =>
				{
					var jObject = JObject.Parse(data);
					var phaseString = jObject.Value<string>("Phase");

					var phase = TumblerPhaseHelpers.GetTumblerPhase(phaseString);
					if (phase != TumblerPhase.InputRegistration) // input registration is executed from tumbling function
					{
						await ExecutePhaseAsync(phase);
					}

					Debug.WriteLine($"New Phase: {phaseString}");
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
		public async Task TumbleAsync(SafeAccount from, SafeAccount to, CancellationToken cancel)
		{
			try
			{
				From = from ?? throw new ArgumentNullException(nameof(from));
				To = to ?? throw new ArgumentNullException(nameof(to));

				// if blocks are not synced yet throw
				var headerHeightResult = await WalletJob.TryGetHeaderHeightAsync();
				if (!headerHeightResult.Success)
				{
					throw new InvalidOperationException("Cannot get header height");
				}
				Height headerHeight = headerHeightResult.Height;
				Height blockHeight = await WalletJob.GetBestHeightAsync();

				if (headerHeight.Value - 2 > blockHeight) // tolerate being maximum two blocks behind
				{
					throw new InvalidOperationException("Blocks are not synced");
				}

				if(TumblerConnection == null)
				{
					await SubscribePhaseChangeAsync();

					if (TumblerConnection == null)
					{
						throw new HttpRequestException("Tumbler is offline");
					}
				}

				StatusResponse status = await TumblerClient.GetStatusAsync(cancel);

				var phase = TumblerPhaseHelpers.GetTumblerPhase(status.Phase);
				
				// wait for input registration
				while(phase != TumblerPhase.InputRegistration)
				{
					await Task.Delay(100, cancel);
				}

				_tumblingInProcess = true;
				await ExecutePhaseAsync(phase);

				// wait while input registration
				while (phase == TumblerPhase.InputRegistration)
				{
					await Task.Delay(100, cancel);
				}

				// wait for input registration again, this is the end of the round (beginning of the next round)
				while (phase != TumblerPhase.InputRegistration)
				{
					await Task.Delay(100, cancel);
				}
				_tumblingInProcess = false;
			}
			catch(OperationCanceledException)
			{
				return;
			}
		}

		private async Task ExecutePhaseAsync(TumblerPhase phase)
		{
			if (_tumblingInProcess)
			{
				if (phase == TumblerPhase.InputRegistration)
				{
					var getBalanceResult = await WalletJob.GetBalanceAsync(From);
					throw new NotImplementedException();
				}
				else if (phase == TumblerPhase.ConnectionConfirmation)
				{
					throw new NotImplementedException();
				}
				else if (phase == TumblerPhase.OutputRegistration)
				{
					throw new NotImplementedException();
				}
				else if (phase == TumblerPhase.Signing)
				{
					throw new NotImplementedException();
				}
				else throw new NotSupportedException("This should never happen");
			}
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
