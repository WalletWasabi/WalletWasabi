using System.Globalization;
using NBitcoin;
using NBitcoin.Payment;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Models;

public record Address
{
	private Address(string btcAddress)
	{
		BtcAddress = btcAddress;
	}

	private Address(string btcAddress, Uri endPoint, decimal amount) : this(btcAddress)
	{
		EndPoint = endPoint;
		Amount = amount;
	}

	public Uri? EndPoint { get; }
	public decimal? Amount { get; }
	public string BtcAddress { get; }

	public static Result<Address> FromRegularAddress(string str, Network network)
	{
		if (IsValidBtcAddress(str, network))
		{
			return new Address(str);
		}

		return "Invalid address";
	}

	private static bool IsValidBtcAddress(string text, Network network)
	{
		text = text.Trim();

		if (text.Length is > 100 or < 20)
		{
			return false;
		}

		try
		{
			BitcoinAddress.Create(text, network);
			return true;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	public static Result<Address> FromPayjoin(string text, Network network)
	{
		text = text.Trim();
		var errorMessage = "Invalid address";

		if (text.Length is > 1000 or < 20)
		{
			return errorMessage;
		}

		try
		{
			if (!text.StartsWith("bitcoin:", true, CultureInfo.InvariantCulture))
			{
				return errorMessage;
			}

			BitcoinUrlBuilder bitcoinUrl = new(text, network);
			if (bitcoinUrl.Address is { } address && address.Network == network)
			{
				if (!bitcoinUrl.UnknownParameters.TryGetValue("pj", out var endpointString))
				{
					return errorMessage;
				}

				if (!Uri.TryCreate(endpointString, UriKind.Absolute, out var endpoint))
				{
					return errorMessage;
				}

				if (bitcoinUrl.Amount is null)
				{
					return errorMessage;
				}

				return new Address(bitcoinUrl.Address.ToString(), endpoint, bitcoinUrl.Amount!.ToDecimal(MoneyUnit.BTC));
			}

			return errorMessage;
		}
		catch (FormatException)
		{
			return errorMessage;
		}
	}
}
