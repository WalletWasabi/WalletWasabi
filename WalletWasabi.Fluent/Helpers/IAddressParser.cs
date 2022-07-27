using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.Helpers;

public interface IAddressParser
{
	Result<Address> GetAddress(string str);
}
