using System.Globalization;
using NBitcoin;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters.Bitcoin;

public class FeeRateSatPerVbJsonConverterNg : JsonConverter<FeeRate>
{
	public override FeeRate? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.Number)
		{
			return null;
		}

		decimal decimalValue = reader.GetDecimal();
		return new FeeRate(decimalValue);
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, FeeRate? value, JsonSerializerOptions options)
	{
		decimal decimalValue = value?.SatoshiPerByte
		                       ?? throw new ArgumentNullException(nameof(value));

		writer.WriteNumberValue(decimalValue);
	}
}
