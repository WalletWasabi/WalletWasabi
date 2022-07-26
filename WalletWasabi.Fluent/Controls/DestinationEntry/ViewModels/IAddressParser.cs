namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public interface IAddressParser
{
	Result<Address> GetAddress(string str);
}
