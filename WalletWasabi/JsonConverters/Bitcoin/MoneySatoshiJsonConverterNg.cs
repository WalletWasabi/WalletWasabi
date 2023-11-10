using NBitcoin;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters.Bitcoin;

public class MoneySatoshiJsonConverterNg : JsonConverter<Money>
{
	/// <inheritdoc />
	public override Money? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.Number)
		{
			throw new InvalidCastException();
		}

		return new Money(reader.GetInt64());
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, Money? value, JsonSerializerOptions options)
	{
		long longValue = value?.Satoshi
			?? throw new ArgumentNullException(nameof(value));
		writer.WriteNumberValue(longValue);
	}
}
