using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.BlockchainAnalysis.Clustering;
using WalletWasabi.Helpers;

namespace WalletWasabi.Transactions.TransactionBuilding
{
	public class DestinationRequest
	{
		public IDestination Destination { get; }
		public MoneyRequest Amount { get; }
		public SmartLabel Label { get; }

		public DestinationRequest(Script scriptPubKey, Money amount, bool subtractFee = false, SmartLabel label = null) : this(scriptPubKey, MoneyRequest.Create(amount, subtractFee), label)
		{
		}

		public DestinationRequest(Script scriptPubKey, MoneyRequest amount, SmartLabel label = null) : this(scriptPubKey.GetDestination(), amount, label)
		{
		}

		public DestinationRequest(IDestination destination, Money amount, bool subtractFee = false, SmartLabel label = null) : this(destination, MoneyRequest.Create(amount, subtractFee), label)
		{
		}

		public DestinationRequest(IDestination destination, MoneyRequest amount, SmartLabel label = null)
		{
			Destination = Guard.NotNull(nameof(destination), destination);
			Amount = Guard.NotNull(nameof(amount), amount);
			Label = label ?? SmartLabel.Empty;
		}
	}
}
