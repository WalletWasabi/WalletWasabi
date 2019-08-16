using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models.TransactionBuilding
{
	public class DestinationRequest
	{
		public IDestination Destination { get; }
		public MoneyRequest Amount { get; }
		public string Label { get; }

		public DestinationRequest(Script scriptPubKey, Money amount, string label = "") : this(scriptPubKey, MoneyRequest.Create(amount), label)
		{
		}

		public DestinationRequest(Script scriptPubKey, MoneyRequest amount, string label = "") : this(scriptPubKey.GetDestination(), amount, label)
		{
		}

		public DestinationRequest(IDestination destination, Money amount, string label = "") : this(destination, MoneyRequest.Create(amount), label)
		{
		}

		public DestinationRequest(IDestination destination, MoneyRequest amount, string label = "")
		{
			Destination = Guard.NotNull(nameof(destination), destination);
			Amount = Guard.NotNull(nameof(amount), amount);
			Label = Guard.Correct(label);
		}
	}
}
