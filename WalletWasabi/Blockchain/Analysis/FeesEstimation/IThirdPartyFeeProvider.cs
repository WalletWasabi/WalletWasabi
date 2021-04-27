using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation
{
	public interface IThirdPartyFeeProvider
	{
		event EventHandler<AllFeeEstimate>? AllFeeEstimateArrived;

		AllFeeEstimate? LastAllFeeEstimate { get; }
		bool InError { get; }
	}
}
