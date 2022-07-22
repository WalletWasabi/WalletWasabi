namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public interface IAddressParser
{
    Address? GetAddress(string str);
}