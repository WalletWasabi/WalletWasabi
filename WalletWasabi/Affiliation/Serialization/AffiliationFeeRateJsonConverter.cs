using Newtonsoft.Json;

namespace WalletWasabi.Affiliation.Serialization;

public class AffiliationFeeRateJsonConverter : JsonConverter<decimal>
{
	public static readonly decimal Base = 1e-8m;

	private static long EncodeDecimal(decimal value)
	{
		return (long)decimal.Round(value / Base);
	}

	private static decimal DecodeDecimal(long value)
	{
		return value * Base;
	}

	private static bool IsEncodable(decimal value)
	{
		return DecodeDecimal(EncodeDecimal(value)) == value;
	}

	public override decimal ReadJson(JsonReader reader, Type objectType, decimal existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is long number)
		{
			return DecodeDecimal(number);
		}
		throw new JsonSerializationException("Cannot deserialize object.");
	}

	public override void WriteJson(JsonWriter writer, decimal value, JsonSerializer serializer)
	{
		if (!IsEncodable(value))
		{
			throw new ArgumentException("Decimal cannot be unambiguously encodable.", nameof(value));
		}
		writer.WriteValue(EncodeDecimal(value));
	}
}
