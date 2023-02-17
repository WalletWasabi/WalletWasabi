using Newtonsoft.Json;
using WalletWasabi.Affiliation.Models.CoinJoinNotification;

namespace WalletWasabi.Affiliation.Serialization;

public class AffiliationCoordinatorFeeRateJsonConverter : JsonConverter<CoordinatorFeeRate>
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

	public override CoordinatorFeeRate ReadJson(JsonReader reader, Type objectType, CoordinatorFeeRate? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is long number)
		{
			return DecodeDecimal(number);
		}

		throw new JsonSerializationException("Cannot deserialize object.");
	}

	public override void WriteJson(JsonWriter writer, CoordinatorFeeRate? value, JsonSerializer serializer)
	{
		if (!IsEncodable(value.FeeRate))
		{
			throw new ArgumentException("Decimal cannot be unambiguously encoded.", nameof(value));
		}

		writer.WriteValue(EncodeDecimal(value.FeeRate));
	}
}
