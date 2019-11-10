using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace WalletWasabi.BlockchainAnalysis.FeesEstimation
{
	public interface IFeeProvider : INotifyPropertyChanged
	{
		public AllFeeEstimate AllFeeEstimate { get; }
	}
}
