using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation
{
	public class FeeProvider : IDisposable
	{
		private volatile bool _disposedValue = false; // To detect redundant calls

		public FeeProvider(WasabiSynchronizer synchronizer, RpcFeeNotifier? rpcNotifier)
		{
			Synchronizer = synchronizer;
			RpcNotifier = rpcNotifier;

			Synchronizer.BestFeeEstimatesArrived += OnBestFeeEstimatesArrived;

			if (RpcNotifier is not null)
			{
				RpcNotifier.BestFeeEstimatesArrived += OnBestFeeEstimatesArrived;
			}
		}

		public event EventHandler<BestFeeEstimates>? BestFeeEstimatesChanged;

		public BestFeeEstimates? BestFeeEstimates { get; private set; }
		private object Lock { get; } = new object();
		public WasabiSynchronizer Synchronizer { get; }
		public RpcFeeNotifier? RpcNotifier { get; }

		private void OnBestFeeEstimatesArrived(object? sender, BestFeeEstimates fees)
		{
			// Only go further if we have estimations.
			if (fees?.Estimations?.Any() is not true)
			{
				return;
			}

			var notify = false;
			lock (Lock)
			{
				if (BestFeeEstimates is null)
				{
					// If the fee was never set yet, then we should set it regardless where it came from.
					notify = TrySetBestFeeEstimates(fees);
				}
				else if (!BestFeeEstimates.IsAccurate && fees.IsAccurate)
				{
					// If the fee was inaccurate and the new fee is accurate, then we should set it regardless where it came from.
					notify = TrySetBestFeeEstimates(fees);
				}
				else if (BestFeeEstimates.IsAccurate && !fees.IsAccurate)
				{
					// If the fee was accurate and the new fee is inaccurate, then we should leave the fees alone.
					return;
				}
				else if (sender is RpcFeeNotifier)
				{
					// If the fee is coming from the user's full node, then set it.
					notify = TrySetBestFeeEstimates(fees);
				}
				else if (sender is WasabiSynchronizer && RpcNotifier is null)
				{
					// If fee is coming from the the backend and user doesn't use a full node, then set the fees.
					notify = TrySetBestFeeEstimates(fees);
				}
				else if (sender is WasabiSynchronizer && RpcNotifier is not null && RpcNotifier.InError is true)
				{
					// If fee is coming from the the backend, user uses a full node, but it doesn't provide fees, then set the fees.
					notify = TrySetBestFeeEstimates(fees);
				}
			}

			if (notify)
			{
				var accuracy = fees.IsAccurate ? "Accurate" : "Inaccurate";
				var from = fees.Estimations.First();
				var to = fees.Estimations.Last();
				Logger.LogInfo($"{accuracy} fee rates are acquired from {sender?.GetType()?.Name} ranging from target {from.Key} at {from.Value} sat/b to target {to.Key} at {to.Value} sat/b.");
				BestFeeEstimatesChanged?.Invoke(this, BestFeeEstimates!);
			}
		}

		private bool TrySetBestFeeEstimates(BestFeeEstimates fees)
		{
			if (BestFeeEstimates != fees)
			{
				BestFeeEstimates = fees;
				return true;
			}
			return false;
		}

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Synchronizer.BestFeeEstimatesArrived -= OnBestFeeEstimatesArrived;

					if (RpcNotifier is not null)
					{
						RpcNotifier.BestFeeEstimatesArrived -= OnBestFeeEstimatesArrived;
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
