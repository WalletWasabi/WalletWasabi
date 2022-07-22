using NBitcoin;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class FullAddressParser : IAddressParser
{
    private readonly BtcAddressValidator btcValidator;
    private readonly PayjoinAddressParser payjoinValidator;

    public FullAddressParser(Network network)
    {
        btcValidator = new BtcAddressValidator(network);
        payjoinValidator = new PayjoinAddressParser(network);
    }

    public Address? GetAddress(string str)
    {
        str = str.Trim();

        if (btcValidator.IsValid(str))
        {
            return new Address(str);
        }

        if (payjoinValidator.TryParse(str, out var payjoinRequest))
        {
            return new Address(payjoinRequest.Address, payjoinRequest.Endpoint, payjoinRequest.Amount);
        }

        return default;
    }
}
