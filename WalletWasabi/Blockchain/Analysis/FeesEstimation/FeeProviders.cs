using System;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation
{
	public class FeeProviders : IFeeProvider, IDisposable
	{
		public FeeProviders(IFeeProvider baseProvider, params IFeeProvider[]? feeProviders)
		{
			Lock = new object();

			Providers = new List<IFeeProvider>();

			if (feeProviders is { })
			{
				Providers.AddRange(feeProviders.Where(x => x is { }));
			}

			// Backend(synchronizer) fee provider is always the last provider.
			Providers.Add(baseProvider);

			foreach (var provider in Providers)
			{
				provider.AllFeeEstimateChanged += Provider_AllFeeEstimateChanged;
			}

			SetAllFeeEstimate();
		}

		public event EventHandler<AllFeeEstimate>? AllFeeEstimateChanged;

		public AllFeeEstimate AllFeeEstimate { get; private set; }

		private List<IFeeProvider> Providers { get; }

		private object Lock { get; }

		private void Provider_AllFeeEstimateChanged(object? sender, AllFeeEstimate e)
		{
			SetAllFeeEstimate();
		}

		private void SetAllFeeEstimate()
		{
			lock (Lock)
			{
				AllFeeEstimate? feeEstimateToSet = null;
				foreach (IFeeProvider provider in Providers.SkipLast(1))
				{
					if (provider.AllFeeEstimate is { IsAccurate: bool isAccurate } af && isAccurate)
					{
						feeEstimateToSet = af;
						break;
					}
				}

				AllFeeEstimate = feeEstimateToSet ?? Providers.Last().AllFeeEstimate;
			}

			AllFeeEstimateChanged?.Invoke(this, AllFeeEstimate);
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					foreach (var provider in Providers)
					{
						provider.AllFeeEstimateChanged -= Provider_AllFeeEstimateChanged;
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
			// Suppress finalization.
			GC.SuppressFinalize(this);
		}

		#endregion IDisposable Support
	}
}
