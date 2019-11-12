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
	public class FeeProviders : NotifyPropertyChangedBase, IFeeProvider, IDisposable
	{
		private IEnumerable<IFeeProvider> Providers { get; }

		private AllFeeEstimate _status;

		public AllFeeEstimate Status
		{
			get => _status;
			private set => RaiseAndSetIfChanged(ref _status, value);
		}

		private object Lock { get; }

		public FeeProviders(IEnumerable<IFeeProvider> feeProviders)
		{
			Providers = feeProviders;
			Lock = new object();

			SetAllFeeEstimate();

			foreach (var provider in Providers)
			{
				provider.PropertyChanged += Provider_PropertyChanged;
			}
		}

		private void Provider_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			IFeeProvider feeProvider;
			if (e.PropertyName == nameof(feeProvider.Status))
			{
				SetAllFeeEstimate();
			}
		}

		private void SetAllFeeEstimate()
		{
			lock (Lock)
			{
				IFeeProvider[] providerArray = Providers.ToArray();
				for (int i = 0; i < providerArray.Length - 1; i++)
				{
					IFeeProvider provider = providerArray[i];
					var allFee = provider.Status;
					if (allFee != null && allFee.IsAccurate)
					{
						Status = allFee;
						return;
					}
				}

				Status = providerArray[providerArray.Length - 1].Status;
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
						provider.PropertyChanged -= Provider_PropertyChanged;
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
