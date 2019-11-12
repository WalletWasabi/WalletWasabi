using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation
{
	public interface IFeeProvider : INotifyPropertyChanged
	{
		public AllFeeEstimate Status { get; }
	}
}
