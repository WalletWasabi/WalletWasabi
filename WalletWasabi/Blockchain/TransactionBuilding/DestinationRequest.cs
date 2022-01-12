using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public class DestinationRequest
{
	public DestinationRequest(Script scriptPubKey, Money amount, bool subtractFee = false, SmartLabel? label = null) : this(scriptPubKey, MoneyRequest.Create(amount, subtractFee), label)
	{
	}

	public DestinationRequest(Script scriptPubKey, MoneyRequest amount, SmartLabel? label = null) : this(scriptPubKey.GetDestination(), amount, label)
	{
	}

	public DestinationRequest(IDestination destination, Money amount, bool subtractFee = false, SmartLabel? label = null) : this(destination, MoneyRequest.Create(amount, subtractFee), label)
	{
	}

	public DestinationRequest(IDestination destination, MoneyRequest amount, SmartLabel? label = null)
	{
		Destination = destination;
		Amount = amount;
		Label = label ?? SmartLabel.Empty;
	}

	public IDestination Destination { get; }
	public MoneyRequest Amount { get; }
	public SmartLabel Label { get; }
}
