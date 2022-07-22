namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public interface IMutableAddressHost
{
    string Text { get; set; }
    IObservable<string> TextChanged { get; }
    IObservable<Address?> ParsedAddress { get; }
}