using NBitcoin;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class BtcOnlyAddressParser : IAddressParser
{
    private readonly BtcAddressValidator btcValidator;

    public BtcOnlyAddressParser(Network network)
    {
        var network1 = network;
        btcValidator = new BtcAddressValidator(network1);
    }

    public Address? GetAddress(string str)
    {
        str = str.Trim();

        if (btcValidator.IsValid(str))
        {
            return new Address(str);
        }

        return default;
    }
}