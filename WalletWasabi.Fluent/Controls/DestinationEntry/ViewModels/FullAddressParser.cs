using NBitcoin;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class FullAddressParser : IAddressParser
{
	private readonly Network _network;

	public FullAddressParser(Network network)
	{
		_network = network;
	}

	public Result<Address> GetAddress(string str)
	{
		str = str.Trim();

		var regular = Address.FromRegularAddress(str, _network);
		if (regular.IsSuccess)
		{
			return regular;
		}

		return Address.FromPayjoin(str, _network);
	}
}
