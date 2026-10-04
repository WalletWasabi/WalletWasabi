using System.Globalization;
using NBitcoin;

namespace WalletWasabi.Mobile;

public sealed record PaymentRequest(BitcoinAddress Address, Money? Amount, string Label, string Message)
{
	public static PaymentRequest Parse(string input, Network network)
	{
		ArgumentNullException.ThrowIfNull(input);
		input = input.Trim();
		if (input.Length is 0 or > 4096)
		{
			throw new FormatException("Enter a Bitcoin address or a bitcoin: payment request.");
		}

		if (!input.StartsWith("bitcoin:", StringComparison.OrdinalIgnoreCase))
		{
			return new(BitcoinAddress.Create(input, network), null, "", "");
		}

		var parts = input[8..].Split('?', 2);
		var address = BitcoinAddress.Create(parts[0], network);
		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (parts.Length == 2)
		{
			foreach (var pair in parts[1].Split('&', StringSplitOptions.RemoveEmptyEntries))
			{
				var kv = pair.Split('=', 2);
				var key = Uri.UnescapeDataString(kv[0]);
				if (key.StartsWith("req-", StringComparison.OrdinalIgnoreCase))
				{
					throw new FormatException($"Unsupported required payment parameter: {key}.");
				}
				if (!values.TryAdd(key, kv.Length == 2 ? Uri.UnescapeDataString(kv[1]) : ""))
				{
					throw new FormatException("The payment request has duplicate parameters.");
				}
			}
		}

		Money? amount = values.TryGetValue("amount", out var value) ? ParseAmount(value) : null;
		return new(address, amount, values.GetValueOrDefault("label", ""), values.GetValueOrDefault("message", ""));
	}

	public static Money ParseAmount(string input)
	{
		if (!decimal.TryParse(input, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
			|| value <= 0 || value > 21_000_000 || decimal.Round(value, 8) != value)
		{
			throw new FormatException("Enter a positive BTC amount with at most 8 decimal places.");
		}
		return Money.Coins(value);
	}

	public string ToUri() => $"bitcoin:{Address}"
		+ (Amount is { } amount ? $"?amount={amount.ToDecimal(MoneyUnit.BTC).ToString("0.########", CultureInfo.InvariantCulture)}" : "")
		+ (Label.Length > 0 ? $"{(Amount is null ? "?" : "&")}label={Uri.EscapeDataString(Label)}" : "");
}
