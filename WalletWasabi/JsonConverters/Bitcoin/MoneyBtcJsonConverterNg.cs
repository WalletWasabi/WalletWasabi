using NBitcoin;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters.Bitcoin;

public class MoneyBtcJsonConverterNg : JsonConverter<Money>
{
	/// <inheritdoc />
	public override Money? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.String)
		{
			return null;
		}

		string? stringValue = reader.GetString();
		return Money.Parse(stringValue);
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, Money? value, JsonSerializerOptions options)
	{
		string stringValue = value?.ToString(fplus: false, trimExcessZero: true)
			?? throw new ArgumentNullException(nameof(value));

		writer.WriteStringValue(stringValue);
	}
}
