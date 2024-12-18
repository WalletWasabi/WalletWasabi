using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Wallets.SilentPayment;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public abstract record Destination
{
	public static implicit operator Destination(BitcoinAddress address) => new Loudly(address.ScriptPubKey);
	public static implicit operator Destination(Script scriptPubKey) => new Loudly(scriptPubKey);

	public string ToString(Network network) =>
		this switch
		{
			Loudly l => l.ScriptPubKey.GetDestinationAddress(network).ToString(),
			Silent s => s.Address.ToWip(network),
			_ => throw new ArgumentException("Unknown Destination type")
		};

	public record Loudly(Script ScriptPubKey) : Destination;

	public record Silent(SilentPaymentAddress Address) : Destination
	{
		public Script FakeScriptPubKey =>
			new PubKey(Address.ScanKey.AddTweak(Address.SpendKey.ToXOnlyPubKey().ToBytes()).ToBytes()).GetScriptPubKey(
				ScriptPubKeyType.TaprootBIP86);
	};
}

public class DestinationRequest(Destination destination, MoneyRequest amount, LabelsArray? labels = null)
{
	public DestinationRequest(Script scriptPubKey, Money amount, bool subtractFee = false, LabelsArray? labels = null)
		: this(scriptPubKey, MoneyRequest.Create(amount, subtractFee), labels)
	{
	}

	public DestinationRequest(Script scriptPubKey, MoneyRequest amount, LabelsArray? labels = null)
		: this(new Destination.Loudly(scriptPubKey), amount, labels)
	{
	}

	public DestinationRequest(IDestination destination, Money amount, bool subtractFee = false, LabelsArray? labels = null)
		: this(new Destination.Loudly(destination.ScriptPubKey), MoneyRequest.Create(amount, subtractFee), labels)
	{
	}

	public DestinationRequest(IDestination destination, MoneyRequest amount, LabelsArray? labels = null)
		: this(new Destination.Loudly(destination.ScriptPubKey), amount, labels)
	{
	}

	public DestinationRequest(SilentPaymentAddress silentPaymentAddress, MoneyRequest amount, LabelsArray? labels = null)
		: this(new Destination.Silent(silentPaymentAddress), amount, labels)
	{
	}

	public Destination Destination { get; } = destination;
	public MoneyRequest Amount { get; } = amount;
	public LabelsArray Labels { get; } = labels ?? LabelsArray.Empty;
}
