using NBitcoin;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.Helpers;

public class BtcOnlyPaymentAddressParser : IPaymentAddressParser
{
	private readonly Network _network;

	public BtcOnlyPaymentAddressParser(Network network)
	{
		_network = network;
	}

	public Result<Address> GetAddress(string str)
	{
		str = str.Trim();

		return Address.FromRegularAddress(str, _network);
	}
}
