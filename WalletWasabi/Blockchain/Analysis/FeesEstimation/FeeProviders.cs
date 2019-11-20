using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using WalletWasabi.Bases;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation
{
	public class FeeProviders : IFeeProvider, IDisposable
	{
		public event EventHandler<AllFeeEstimate> AllFeeEstimateChanged;

		public AllFeeEstimate AllFeeEstimate { get; private set; }

		private IEnumerable<IFeeProvider> Providers { get; }

		private object Lock { get; }

		public FeeProviders(IEnumerable<IFeeProvider> feeProviders)
		{
			Providers = feeProviders;
			Lock = new object();

			SetAllFeeEstimate();

			foreach (var provider in Providers)
			{
				provider.AllFeeEstimateChanged += Provider_AllFeeEstimateChanged;
			}
		}

		private void Provider_AllFeeEstimateChanged(object sender, AllFeeEstimate e)
		{
			SetAllFeeEstimate();
		}

		private void SetAllFeeEstimate()
		{
			lock (Lock)
			{
				IFeeProvider[] providerArray = Providers.ToArray();
				for (int i = 0; i < providerArray.Length - 1; i++)
				{
					IFeeProvider provider = providerArray[i];
					var af = provider.AllFeeEstimate;
					if (af != null && af.IsAccurate && af != AllFeeEstimate)
					{
						AllFeeEstimate = af;
						AllFeeEstimateChanged?.Invoke(this, af);
						return;
					}
				}

				var allFee = providerArray[^1].AllFeeEstimate;
				if (allFee != AllFeeEstimate)
				{
					AllFeeEstimate = allFee;
					AllFeeEstimateChanged?.Invoke(this, allFee);
					return;
				}
			}
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
