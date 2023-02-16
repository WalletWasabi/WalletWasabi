using System.Collections.Generic;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public class MacJsonConverter : JsonConverter<MAC>
{
 	public override MAC? ReadJson(JsonReader reader, Type objectType, MAC? existingValue, bool hasExistingValue, JsonSerializer serializer)
 	{
	    if (reader.TokenType != JsonToken.StartObject)
	    {
		    throw new JsonException();
	    }

	    var t = ReadProperty<Scalar>(reader, serializer, "T");
	    var V = ReadProperty<GroupElement>(reader, serializer, "V");
	    reader.Read();
	    if (reader.TokenType == JsonToken.EndObject)
	    {
		    return ReflectionUtils.CreateInstance<MAC>(new object[]{ t, V });
	    }

 		throw new ArgumentException($"No valid serialized {nameof(MAC)} passed.");
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
 	public override void WriteJson(JsonWriter writer, MAC? value, JsonSerializer serializer)
 	{
 		if (value is { } mac)
 		{
 			writer.WriteStartObject();
	        writer.WritePropertyName("T");
	        serializer.Serialize(writer, mac.T);
	        writer.WritePropertyName("V");
	        serializer.Serialize(writer, mac.V);
	        writer.WriteEndObject();
 			return;
 		}
 		throw new ArgumentException($"No valid {nameof(MAC)}.", nameof(value));
 	}
}
