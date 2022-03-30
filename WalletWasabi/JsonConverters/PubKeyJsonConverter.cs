using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters;

public class PubKeyJsonConverter : JsonConverter<PubKey>
{
	/// <inheritdoc />
	public override PubKey? ReadJson(JsonReader reader, Type objectType, PubKey? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		string? hex = ((string?)reader.Value)?.Trim();

		if (hex is null)
		{
			throw new ArgumentNullException(nameof(hex));
		}

		return new PubKey(hex);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, PubKey? value, JsonSerializer serializer)
	{
		var pubKeyHex = value?.ToHex() ?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(pubKeyHex);
	}
}
