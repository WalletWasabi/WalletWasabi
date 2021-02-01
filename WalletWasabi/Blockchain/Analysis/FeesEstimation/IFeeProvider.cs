using System;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation
{
	public interface IFeeProvider
	{
		public event EventHandler<BestFeeEstimates>? AllFeeEstimateChanged;

		public BestFeeEstimates AllFeeEstimate { get; }
	}
}
