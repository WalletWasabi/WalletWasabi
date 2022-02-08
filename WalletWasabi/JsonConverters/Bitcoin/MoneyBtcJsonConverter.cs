using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters.Bitcoin;

public class MoneyBtcJsonConverter : JsonConverter<Money>
{
	/// <inheritdoc />
	public override Money? ReadJson(JsonReader reader, Type objectType, Money? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var stringValue = reader.Value as string;
		return Parse(stringValue);
	}

	public static Money? Parse(string? stringValue)
	{
		if (string.IsNullOrWhiteSpace(stringValue))
		{
			return null;
		}
		else
		{
			return Money.Parse(stringValue);
		}
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, Money? value, JsonSerializer serializer)
	{
		var stringValue = value?.ToString(fplus: false, trimExcessZero: true) ?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(stringValue);
	}
}
