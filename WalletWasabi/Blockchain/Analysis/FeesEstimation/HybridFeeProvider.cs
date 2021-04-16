using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation
{
	/// <summary>
	/// Manages multiple fee sources. Returns the best one.
	/// Prefers local full node, as long as the fee is accurate.
	/// </summary>
	public class HybridFeeProvider : IDisposable
	{
		private volatile bool _disposedValue = false; // To detect redundant calls

		public HybridFeeProvider(WasabiSynchronizer synchronizer, RpcFeeProvider? rpcFeeProvider)
		{
			Synchronizer = synchronizer;
			RpcFeeProvider = rpcFeeProvider;

			Synchronizer.ResponseArrived += Synchronizer_ResponseArrived;

			if (RpcFeeProvider is not null)
			{
				RpcFeeProvider.AllFeeEstimateArrived += OnAllFeeEstimateArrived;
			}

			var syncerEstimate = Synchronizer.LastResponse?.AllFeeEstimate;
			var rpcEstimate = RpcFeeProvider?.LastAllFeeEstimate;
			var betterEstimate = rpcEstimate?.IsAccurate is true ? rpcEstimate : syncerEstimate;
			if (betterEstimate is not null)
			{
				SetAllFeeEstimate(betterEstimate);
			}
		}

		public event EventHandler<AllFeeEstimate>? AllFeeEstimateChanged;

		public WasabiSynchronizer Synchronizer { get; }
		public RpcFeeProvider? RpcFeeProvider { get; }
		private object Lock { get; } = new object();
		public AllFeeEstimate? AllFeeEstimate { get; private set; }

		private void Synchronizer_ResponseArrived(object? sender, SynchronizeResponse response)
		{
			OnAllFeeEstimateArrived(sender, response.AllFeeEstimate);
		}

		private void OnAllFeeEstimateArrived(object? sender, AllFeeEstimate? fees)
		{
			// Only go further if we have estimations.
			if (fees?.Estimations?.Any() is not true)
			{
				return;
			}

			var notify = false;
			lock (Lock)
			{
				if (AllFeeEstimate is null)
				{
					// If the fee was never set yet, then we should set it regardless where it came from.
					notify = SetAllFeeEstimate(fees);
				}
				else if (!AllFeeEstimate.IsAccurate && fees.IsAccurate)
				{
					// If the fee was inaccurate and the new fee is accurate, then we should set it regardless where it came from.
					notify = SetAllFeeEstimate(fees);
				}
				else if (AllFeeEstimate.IsAccurate && !fees.IsAccurate)
				{
					// If the fee was accurate and the new fee is inaccurate, then we should leave the fees alone.
					return;
				}
				else if (sender is RpcFeeProvider)
				{
					// If the fee is coming from the user's full node, then set it.
					notify = SetAllFeeEstimate(fees);
				}
				else if (sender is WasabiSynchronizer && RpcFeeProvider is null)
				{
					// If fee is coming from the the backend and user doesn't use a full node, then set the fees.
					notify = SetAllFeeEstimate(fees);
				}
				else if (sender is WasabiSynchronizer && RpcFeeProvider is not null && RpcFeeProvider.InError is true)
				{
					// If fee is coming from the the backend, user uses a full node, but it doesn't provide fees, then set the fees.
					notify = SetAllFeeEstimate(fees);
				}
			}

			if (notify)
			{
				var accuracy = fees.IsAccurate ? "Accurate" : "Inaccurate";
				var from = fees.Estimations.First();
				var to = fees.Estimations.Last();
				Logger.LogInfo($"{accuracy} fee rates are acquired from {sender?.GetType()?.Name} ranging from target {from.Key} at {from.Value} sat/b to target {to.Key} at {to.Value} sat/b.");
				AllFeeEstimateChanged?.Invoke(this, AllFeeEstimate!);
			}
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

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Synchronizer.ResponseArrived -= Synchronizer_ResponseArrived;

					if (RpcFeeProvider is not null)
					{
						RpcFeeProvider.AllFeeEstimateArrived -= OnAllFeeEstimateArrived;
					}
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
