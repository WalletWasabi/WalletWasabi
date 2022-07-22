using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NBitcoin;
using NBitcoin.Payment;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class PayjoinAddressParser
{
    private readonly Network expectedNetwork;

    public PayjoinAddressParser(Network expectedNetwork)
    {
        this.expectedNetwork = expectedNetwork;
    }

    public bool TryParse(string text, [NotNullWhen(true)] out PayjoinRequest? payjoinRequest)
    {
        payjoinRequest = null;

        if (text is null || expectedNetwork is null)
        {
            return false;
        }

        text = text.Trim();

        if (text.Length is > 1000 or < 20)
        {
            return false;
        }

        try
        {
            if (!text.StartsWith("bitcoin:", true, CultureInfo.InvariantCulture))
            {
                return false;
            }

            BitcoinUrlBuilder bitcoinUrl = new(text, expectedNetwork);
            if (bitcoinUrl.Address is { } address && address.Network == expectedNetwork)
            {
                if (!bitcoinUrl.UnknownParameters.TryGetValue("pj", out var endpointString))
                {
                    return false;
                }

                if (!Uri.TryCreate(endpointString, UriKind.Absolute, out var endpoint))
                {
                    return false;
                }

                if (bitcoinUrl.Amount is null)
                {
                    return false;
                }

                payjoinRequest = new PayjoinRequest(endpoint, bitcoinUrl.Address.ToString(), bitcoinUrl.Amount!.ToDecimal(MoneyUnit.BTC));
                return true;
            }

            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}