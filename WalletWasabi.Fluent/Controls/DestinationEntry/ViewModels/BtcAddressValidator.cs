using NBitcoin;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class BtcAddressValidator
{
    private readonly Network expectedNetwork;

    public BtcAddressValidator(Network expectedNetwork)
    {
        this.expectedNetwork = expectedNetwork ?? throw new ArgumentNullException(nameof(expectedNetwork));
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
            NBitcoin.BitcoinAddress.Create(text, expectedNetwork);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}