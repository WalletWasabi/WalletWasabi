using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Helpers;

namespace WalletWasabi.Rpc.JsonConverters;

public class BitcoinAddressJsonConverter : JsonConverter<BitcoinAddress>
{
	/// <inheritdoc />
	public override BitcoinAddress? ReadJson(JsonReader reader, Type objectType, BitcoinAddress? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var bitcoinAddressString = reader.Value as string;
		if (string.IsNullOrWhiteSpace(bitcoinAddressString))
		{
			return default;
		}
		else
		{
			return BetterParseBitcoinAddress(bitcoinAddressString);
		}
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, BitcoinAddress? value, JsonSerializer serializer)
	{
		var stringValue = value?.ToString() ?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(stringValue);
	}

	private BitcoinAddress BetterParseBitcoinAddress(string bitcoinAddressString)
	{
		bitcoinAddressString = Guard.NotNullOrEmptyOrWhitespace(nameof(bitcoinAddressString), bitcoinAddressString, trim: true);

		BitcoinAddress ba;
		try
		{
			ba = BitcoinAddress.Create(bitcoinAddressString, Network.Main);
		}
		catch
		{
			try
			{
				ba = BitcoinAddress.Create(bitcoinAddressString, Network.TestNet);
			}
			catch
			{
				ba = BitcoinAddress.Create(bitcoinAddressString, Network.RegTest);
			}
		}

		return ba;
	}
}
