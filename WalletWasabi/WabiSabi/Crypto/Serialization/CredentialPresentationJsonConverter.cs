 using Newtonsoft.Json;
 using WabiSabi.Crypto.Groups;
 using WabiSabi.Crypto.ZeroKnowledge;
 using WalletWasabi.JsonConverters;

 namespace WalletWasabi.WabiSabi.Crypto.Serialization;
 
 public class CredentialPresentationJsonConverter : JsonConverter<CredentialPresentation>
 {
 	public override CredentialPresentation? ReadJson(JsonReader reader, Type objectType, CredentialPresentation? existingValue, bool hasExistingValue, JsonSerializer serializer)
 	{
	    if (reader.TokenType != JsonToken.StartObject)
	    {
		    throw new JsonException();
	    }

	    var ca = ReadProperty<GroupElement>(reader, serializer, "Ca");
	    var cx0 = ReadProperty<GroupElement>(reader, serializer, "Cx0");
	    var cx1 = ReadProperty<GroupElement>(reader, serializer, "Cx1");
	    var cV = ReadProperty<GroupElement>(reader, serializer, "CV");
	    var s = ReadProperty<GroupElement>(reader, serializer, "S");
	    reader.Read();
	    if (reader.TokenType == JsonToken.EndObject)
	    {
		    return ReflectionUtils.CreateInstance<CredentialPresentation>(new object[]{ ca, cx0, cx1, cV, s });
	    }

 		throw new ArgumentException($"No valid serialized {nameof(CredentialPresentation)} passed.");
 	}

    private T? ReadProperty<T>(JsonReader reader, JsonSerializer serializer, string name)
    {
	    if (!reader.Read())
	    {
		    throw new JsonException($"Property '{name}' was expected.");
	    }

	    if (reader.TokenType == JsonToken.PropertyName)
	    {
		    var propertyName = reader.Value.ToString();
		    if (propertyName != name)
		    {
			    throw new JsonException($"Property '{name}' was expected.");
		    }

		    reader.Read();
		    return serializer.Deserialize<T>(reader);
	    }
		throw new JsonException($"Property '{name}' was expected.");
    }

    /// <inheritdoc />
 	public override void WriteJson(JsonWriter writer, CredentialPresentation? value, JsonSerializer serializer)
 	{
 		if (value != null)
 		{
 			writer.WriteStartObject();
	        writer.WritePropertyName("Ca");
	        serializer.Serialize(writer, value.Ca);
	        writer.WritePropertyName("Cx0");
	        serializer.Serialize(writer, value.Cx0);
	        writer.WritePropertyName("Cx1");
	        serializer.Serialize(writer, value.Cx1);
	        writer.WritePropertyName("CV");
	        serializer.Serialize(writer, value.CV);
	        writer.WritePropertyName("S");
	        serializer.Serialize(writer, value.S);
	        writer.WriteEndObject();
 			return;
 		}
 		throw new ArgumentException($"No valid {nameof(CredentialPresentation)}.", nameof(value));
 	}
 }
 