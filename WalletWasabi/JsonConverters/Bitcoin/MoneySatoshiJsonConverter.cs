using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters.Bitcoin;

public class MoneySatoshiJsonConverter : JsonConverter<Money>
{
	/// <inheritdoc />
	public override Money? ReadJson(JsonReader reader, Type objectType, Money? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var serialized = (long?)reader.Value;

		return serialized is null ? null : new Money(serialized.Value);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, Money? value, JsonSerializer serializer)
	{
		long longValue = value?.Satoshi
			?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(longValue);
	}
}
