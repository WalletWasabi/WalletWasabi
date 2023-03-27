using System.Diagnostics.CodeAnalysis;
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
		var ca = reader.ReadProperty<GroupElement>(serializer, "ca");
		var cx0 = reader.ReadProperty<GroupElement>(serializer, "cx0");
		var cx1 = reader.ReadProperty<GroupElement>(serializer, "cx1");
		var cV = reader.ReadProperty<GroupElement>(serializer, "cv");
		var s = reader.ReadProperty<GroupElement>(serializer, "s");
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
		writer.WriteProperty("ca", value.Ca, serializer);
		writer.WriteProperty("cx0", value.Cx0, serializer);
		writer.WriteProperty("cx1", value.Cx1, serializer);
		writer.WriteProperty("cv", value.CV, serializer);
		writer.WriteProperty("s", value.S, serializer);
		writer.WriteEndObject();
	}
}
