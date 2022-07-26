using NBitcoin;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class BtcOnlyAddressParser : IAddressParser
{
	private readonly Network network;

	public BtcOnlyAddressParser(Network network)
	{
		this.network = network;
	}

	public Result<Address> GetAddress(string str)
	{
		str = str.Trim();

		return Address.FromRegularAddress(str, network);
	}
}
