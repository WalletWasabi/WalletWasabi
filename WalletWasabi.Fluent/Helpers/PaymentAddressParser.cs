using NBitcoin;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.Helpers;

public class PaymentAddressParser : IPaymentAddressParser
{
	private readonly Network _network;

	public PaymentAddressParser(Network network)
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
