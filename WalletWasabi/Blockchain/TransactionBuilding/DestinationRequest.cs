using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public class DestinationRequest
{
	public DestinationRequest(Script scriptPubKey, Money amount, bool subtractFee = false, LabelsArray? labels = null) : this(scriptPubKey, MoneyRequest.Create(amount, subtractFee), labels)
	{
	}

	public DestinationRequest(Script scriptPubKey, MoneyRequest amount, LabelsArray? labels = null) : this(scriptPubKey.GetDestination(), amount, labels)
	{
	}

	public DestinationRequest(IDestination destination, Money amount, bool subtractFee = false, LabelsArray? labels = null) : this(destination, MoneyRequest.Create(amount, subtractFee), labels)
	{
	}

	public DestinationRequest(IDestination destination, MoneyRequest amount, LabelsArray? labels = null)
	{
		Destination = destination;
		Amount = amount;
		Labels = labels ?? LabelsArray.Empty;
	}

	public IDestination Destination { get; }
	public MoneyRequest Amount { get; }
	public LabelsArray Labels { get; }
}
