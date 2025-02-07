using Newtonsoft.Json;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public class CredentialPresentationJsonConverter : JsonConverter<CredentialPresentation>
{
	public override CredentialPresentation? ReadJson(JsonReader reader, Type objectType, CredentialPresentation? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		reader.Expect(JsonToken.StartObject);
		var ca = reader.ReadProperty<GroupElement>(serializer, "Ca");
		var cx0 = reader.ReadProperty<GroupElement>(serializer, "Cx0");
		var cx1 = reader.ReadProperty<GroupElement>(serializer, "Cx1");
		var cV = reader.ReadProperty<GroupElement>(serializer, "CV");
		var s = reader.ReadProperty<GroupElement>(serializer, "S");
		reader.Read();
		reader.Expect(JsonToken.EndObject);

		return ReflectionUtils.CreateInstance<CredentialPresentation>(new object[] { ca, cx0, cx1, cV, s });
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, CredentialPresentation? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			throw new ArgumentException($"No valid {nameof(CredentialPresentation)}.", nameof(value));
		}
		writer.WriteStartObject();
		writer.WriteProperty("Ca", value.Ca, serializer);
		writer.WriteProperty("Cx0", value.Cx0, serializer);
		writer.WriteProperty("Cx1", value.Cx1, serializer);
		writer.WriteProperty("CV", value.CV, serializer);
		writer.WriteProperty("S", value.S, serializer);
		writer.WriteEndObject();
	}
}
