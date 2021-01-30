using System;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation
{
	public class PrivacyFeeProvider : IFeeProvider, IDisposable
	{
		public PrivacyFeeProvider(IFeeProvider baseProvider, params IFeeProvider[]? feeProviders)
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

			SetEstimate();
		}

		public event EventHandler<AllFeeEstimate>? AllFeeEstimateChanged;

		public AllFeeEstimate AllFeeEstimate { get; private set; }

		private List<IFeeProvider> Providers { get; }

		private object Lock { get; }

		private void Provider_AllFeeEstimateChanged(object? sender, AllFeeEstimate e)
		{
			SetEstimate();
		}

		private void SetEstimate()
		{
			lock (Lock)
			{
				AllFeeEstimate? bestProviderEstimate = null;
				foreach (IFeeProvider provider in Providers.SkipLast(1))
				{
					if (provider.AllFeeEstimate is { IsAccurate: bool isAccurate } af && isAccurate)
					{
						bestProviderEstimate = af;
						break;
					}
				}
				bestProviderEstimate ??= Providers.Last().AllFeeEstimate;

				var privacyEstimate = bestProviderEstimate?.Unfingerprint();
				if (privacyEstimate is { })
				{
					AllFeeEstimate = privacyEstimate;
				}
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
		}

		#endregion IDisposable Support
	}
}
