using System;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation
{
	public interface IFeeProvider
	{
		public event EventHandler<AllFeeEstimate>? AllFeeEstimateChanged;

		public AllFeeEstimate AllFeeEstimate { get; }
	}
}
