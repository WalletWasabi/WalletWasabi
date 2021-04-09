using NBitcoin;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction
{
	public interface IState
	{
		public Money Balance { get; }
		public int EstimatedInputsVsize { get; }
		public int OutputsVsize { get; }
		public int EstimatedVsize { get; }
		public FeeRate EffectiveFeeRate { get; }
	}
}
