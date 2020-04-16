using System;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;

namespace WalletWasabi.Services
{
	public class StoredFeeEstimateProvider : IFeeProvider
	{
		public StoredFeeEstimateProvider(AllFeeEstimate savedEstimate)
		{
			AllFeeEstimate = savedEstimate;
		}
		public event EventHandler<AllFeeEstimate> AllFeeEstimateChanged;
		public AllFeeEstimate AllFeeEstimate { get; }
	}
}