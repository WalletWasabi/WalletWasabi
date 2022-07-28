using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.Helpers;

public interface IPaymentAddressParser
{
	Result<Address> GetAddress(string str);
}
