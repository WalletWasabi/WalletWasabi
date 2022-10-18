using Newtonsoft.Json;

namespace WalletWasabi.Affiliation.Serialization;

public class AffiliationFlagJsonConverter : JsonConverter<AffiliationFlag>
{
	public override AffiliationFlag? ReadJson(JsonReader reader, Type objectType, AffiliationFlag? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			return new AffiliationFlag(serialized);
		}
		else
		{
			throw new Exception();
		}
	}

	public override void WriteJson(JsonWriter writer, AffiliationFlag? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			throw new ArgumentNullException(nameof(value));
		}
		else
		{
			writer.WriteValue(value.Name);
		}
	}
}
