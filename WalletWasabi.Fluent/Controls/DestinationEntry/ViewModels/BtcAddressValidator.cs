using NBitcoin;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class BtcAddressValidator
{
    private readonly Network _expectedNetwork;

    public BtcAddressValidator(Network expectedNetwork)
    {
        _expectedNetwork = expectedNetwork ?? throw new ArgumentNullException(nameof(expectedNetwork));
    }

    public bool IsValid(string text)
    {
        text = text.Trim();

        if (text.Length is > 100 or < 20)
        {
            return false;
        }

        try
        {
            BitcoinAddress.Create(text, _expectedNetwork);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
