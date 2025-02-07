using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters;

public class KeyJsonConverter : JsonConverter<Key>
{
	/// <inheritdoc />
	public override Key? ReadJson(JsonReader reader, Type objectType, Key? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var keyString = reader.Value as string;
		if (string.IsNullOrWhiteSpace(keyString))
		{
			return default;
		}
		else
		{
			return NBitcoinHelpers.BetterParseKey(keyString);
		}
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, Key? value, JsonSerializer serializer)
	{
		BitcoinSecret bitcoinSecret = value?.GetWif(Network.Main) ?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(bitcoinSecret);
	}
}
