using Newtonsoft.Json;
using WalletWasabi.Helpers;

namespace WalletWasabi.Affiliation.Serialization;

public class AffiliationFlagJsonConverter : JsonConverter<AffiliationFlag>
{
	public override AffiliationFlag? ReadJson(JsonReader reader, Type objectType, AffiliationFlag? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			return new AffiliationFlag(serialized);
		}
		throw new JsonSerializationException("Cannot deserialize object.");
	}

	public override void WriteJson(JsonWriter writer, AffiliationFlag? value, JsonSerializer serializer)
	{
		Guard.NotNull(nameof(value), value);
		writer.WriteValue(value.Name);
	}
}
