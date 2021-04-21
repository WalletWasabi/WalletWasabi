using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Models;
using WalletWasabi.Nito.AsyncEx;
using WalletWasabi.Services;
using WalletWasabi.WebClients.BlockstreamInfo;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation
{
	public class ThirdPartyFeeProvider : IHostedService
	{
		public ThirdPartyFeeProvider(WasabiSynchronizer synchronizer, BlockstreamInfoFreeProvider blockstreamProvider)
		{
			Synchronizer = synchronizer;
			BlockstreamProvider = blockstreamProvider;
		}

		public event EventHandler<AllFeeEstimate>? AllFeeEstimateArrived;

		public WasabiSynchronizer Synchronizer { get; }
		public BlockstreamInfoFreeProvider BlockstreamProvider { get; }
		public AllFeeEstimate? LastAllFeeEstimate { get; private set; }
		private object Lock { get; } = new object();
		public bool InError { get; private set; }
		private AbandonedTasks ProcessingEvents { get; } = new();

		public Task StartAsync(CancellationToken cancellationToken)
		{
			SetAllFeeEstimateIfLooksBetter(Synchronizer.LastResponse?.AllFeeEstimate);
			SetAllFeeEstimateIfLooksBetter(BlockstreamProvider.LastAllFeeEstimate);

			Synchronizer.ResponseArrived += Synchronizer_ResponseArrived;
			BlockstreamProvider.AllFeeEstimateArrived += OnAllFeeEstimateArrived;

			return Task.CompletedTask;
		}

		public async Task StopAsync(CancellationToken cancellationToken)
		{
			Synchronizer.ResponseArrived -= Synchronizer_ResponseArrived;
			BlockstreamProvider.AllFeeEstimateArrived -= OnAllFeeEstimateArrived;

			await ProcessingEvents.WhenAllAsync().ConfigureAwait(false);
		}

		private void Synchronizer_ResponseArrived(object? sender, SynchronizeResponse e)
		{
			OnAllFeeEstimateArrived(sender, e.AllFeeEstimate);
		}

		private void OnAllFeeEstimateArrived(object? sender, AllFeeEstimate? fees)
		{
			using (RunningTasks.RememberWith(ProcessingEvents))
			{
				// Only go further if we have estimations.
				if (fees?.Estimations?.Any() is not true)
				{
					return;
				}

				var notify = false;
				lock (Lock)
				{
					if (LastAllFeeEstimate is null)
					{
						// If it wasn't set before, then set it regardless everything.
						notify = SetAllFeeEstimate(fees);
					}
					else if (sender is BlockstreamInfoFreeProvider)
					{
						var syncronizer = Synchronizer;

						if (syncronizer.LastResponse?.AllFeeEstimate?.IsAccurate is true && syncronizer.BackendStatus == BackendStatus.Connected)
						{
							// If backend is properly serving data, then we don't care about this event.
							return;
						}
						else
						{
							if (fees.IsAccurate)
							{
								// If the backend is not properly serving accurate data then, this is the best we got.
								notify = SetAllFeeEstimate(fees);
							}
							else
							{
								// If third parties are not ready, then let's try our best effort figuring out which data looks better:

								notify = SetAllFeeEstimateIfLooksBetter(fees);
							}
						}
					}
					else if (sender is WasabiSynchronizer)
					{
						if (fees.IsAccurate)
						{
							// If the backend is properly serving data, we're done here.
							notify = SetAllFeeEstimate(fees);
						}
						else
						{
							if (ThirdPartyFeeProvider.InError)
							{
								// If the user's full node isn't ready, but the backend is, then let's leave it to the backend.
								return;
							}
							else
							{
								// If neither user's full node, nor backend is ready, then let's try our best effort figuring out which data looks better:
								notify = SetAllFeeEstimateIfLooksBetter(fees);
							}
						}
					}
				}

				if (notify)
				{
					var accuracy = fees.IsAccurate ? "Accurate" : "Inaccurate";
					var from = fees.Estimations.First();
					var to = fees.Estimations.Last();
					Logger.LogInfo($"{accuracy} fee rates are acquired from {sender?.GetType()?.Name} ranging from target {from.Key} at {from.Value} sat/b to target {to.Key} at {to.Value} sat/b.");
					AllFeeEstimateChanged?.Invoke(this, fees);
				}
			}
		}

		private bool SetAllFeeEstimateIfLooksBetter(AllFeeEstimate? fees)
		{
			var current = AllFeeEstimate;
			if (fees is null
				|| fees == current
				|| (current is not null && ((!fees.IsAccurate && current.IsAccurate) || fees.Estimations.Count <= current.Estimations.Count)))
			{
				return false;
			}
			return SetAllFeeEstimate(fees);
		}

		/// <returns>True if changed.</returns>
		private bool SetAllFeeEstimate(AllFeeEstimate fees)
		{
			if (AllFeeEstimate == fees)
			{
				return false;
			}
			AllFeeEstimate = fees;
			return true;
		}
	}
}
