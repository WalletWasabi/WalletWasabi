using System.Collections.Generic;
using Newtonsoft.Json;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public class ProofJsonConverter : JsonConverter<Proof>
{
 	public override Proof? ReadJson(JsonReader reader, Type objectType, Proof? existingValue, bool hasExistingValue, JsonSerializer serializer)
 	{
	    if (reader.TokenType != JsonToken.StartObject)
	    {
		    throw new JsonException();
	    }

	    var publicNonces = ReadProperty<GroupElementVector>(reader, serializer, "PublicNonces");
	    var responses = ReadProperty<ScalarVector>(reader, serializer, "Responses");
	    reader.Read();
	    if (reader.TokenType == JsonToken.EndObject)
	    {
		    return ReflectionUtils.CreateInstance<Proof>(new object[]{ publicNonces, responses });
	    }

 		throw new ArgumentException($"No valid serialized {nameof(Proof)} passed.");
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
 	public override void WriteJson(JsonWriter writer, Proof? value, JsonSerializer serializer)
 	{
 		if (value is { } proof)
 		{
 			writer.WriteStartObject();
	        writer.WritePropertyName("PublicNonces");
	        serializer.Serialize(writer, proof.PublicNonces);
	        writer.WritePropertyName("Responses");
	        serializer.Serialize(writer, proof.Responses);
	        writer.WriteEndObject();
 			return;
 		}
 		throw new ArgumentException($"No valid {nameof(Proof)}.", nameof(value));
 	}
}
