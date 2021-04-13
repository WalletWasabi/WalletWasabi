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
		public FeeProvider(WasabiSynchronizer synchronizer, RpcFeeNotifier? rpcNotifier)
		{
			Synchronizer = synchronizer;
			RpcNotifier = rpcNotifier;

			Synchronizer.AllFeeEstimateArrived += OnAllFeeEstimateArrived;

			if (RpcNotifier is not null)
			{
				RpcNotifier.AllFeeEstimateArrived += OnAllFeeEstimateArrived;
			}
		}

		public event EventHandler<AllFeeEstimate>? AllFeeEstimateChanged;

		public AllFeeEstimate? AllFeeEstimate { get; private set; }
		private object Lock { get; } = new object();
		public WasabiSynchronizer Synchronizer { get; }
		public RpcFeeNotifier? RpcNotifier { get; }

		private void OnAllFeeEstimateArrived(object? sender, AllFeeEstimate fees)
		{
			// Only go further if we have estimations.
			if (fees?.Estimations?.Any() is not true)
			{
				return;
			}

			var notify = false;
			lock (Lock)
			{
				// If the fee was never set yet, then we should set it regardless where it came from.
				if (AllFeeEstimate is null)
				{
					notify = TrySetAllFeeEstimate(fees);
				}
				// If the fee was inaccurate and the new fee is accurate, then we should set it regardless where it came from.
				else if (!AllFeeEstimate.IsAccurate && fees.IsAccurate)
				{
					notify = TrySetAllFeeEstimate(fees);
				}
				// If the fee was accurate and the new fee is inaccurate, then we should leave the fees alone.
				else if (AllFeeEstimate.IsAccurate && !fees.IsAccurate)
				{
					return;
				}
				// If the fee is coming from the user's full node, then set it.
				else if (sender is RpcFeeNotifier)
				{
					notify = TrySetAllFeeEstimate(fees);
				}
				// If fee is coming from the the backend and user doesn't use a full node, then set the fees.
				else if (sender is WasabiSynchronizer && RpcNotifier is null)
				{
					notify = TrySetAllFeeEstimate(fees);
				}
				// If fee is coming from the the backend, user uses a full node, but it doesn't provide fees, then set the fees.
				else if (sender is WasabiSynchronizer && RpcNotifier is not null && RpcNotifier.InError is true)
				{
					notify = TrySetAllFeeEstimate(fees);
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

		private bool TrySetAllFeeEstimate(AllFeeEstimate fees)
		{
			if (AllFeeEstimate != fees)
			{
				AllFeeEstimate = fees;
				return true;
			}
			return false;
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Synchronizer.AllFeeEstimateArrived -= OnAllFeeEstimateArrived;

					if (RpcNotifier is not null)
					{
						RpcNotifier.AllFeeEstimateArrived -= OnAllFeeEstimateArrived;
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
