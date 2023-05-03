using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public class DestinationRequest
{
	public DestinationRequest(Script scriptPubKey, Money amount, bool subtractFee = false, LabelsArray? label = null) : this(scriptPubKey, MoneyRequest.Create(amount, subtractFee), label)
	{
	}

	public DestinationRequest(Script scriptPubKey, MoneyRequest amount, LabelsArray? label = null) : this(scriptPubKey.GetDestination(), amount, label)
	{
	}

	public DestinationRequest(IDestination destination, Money amount, bool subtractFee = false, LabelsArray? label = null) : this(destination, MoneyRequest.Create(amount, subtractFee), label)
	{
	}

	public DestinationRequest(IDestination destination, MoneyRequest amount, LabelsArray? label = null)
	{
		Destination = destination;
		Amount = amount;
		Label = label ?? LabelsArray.Empty;
	}

	public IDestination Destination { get; }
	public MoneyRequest Amount { get; }
	public LabelsArray Label { get; }
}
