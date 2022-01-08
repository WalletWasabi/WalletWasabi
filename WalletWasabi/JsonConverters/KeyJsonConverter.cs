using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters;

public class KeyJsonConverter : JsonConverter
{
	/// <inheritdoc />
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(Key);
	}

	/// <inheritdoc />
	public override object? ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
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
	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		var key = (Key)value;
		writer.WriteValue(key.GetWif(Network.Main));
	}
}
