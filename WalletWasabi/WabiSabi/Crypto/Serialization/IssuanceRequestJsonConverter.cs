 using System.Collections.Generic;
 using Newtonsoft.Json;
 using WabiSabi;
 using WabiSabi.Crypto.Groups;
 using WalletWasabi.JsonConverters;

 namespace WalletWasabi.WabiSabi.Crypto.Serialization;
 
 public class IssuanceRequestJsonConverter : JsonConverter<IssuanceRequest>
 {
 	public override IssuanceRequest? ReadJson(JsonReader reader, Type objectType, IssuanceRequest? existingValue, bool hasExistingValue, JsonSerializer serializer)
 	{
	    if (reader.TokenType != JsonToken.StartObject)
	    {
		    throw new JsonException();
	    }

	    var ma = ReadProperty<GroupElement>(reader, serializer, "Ma");
	    var bitCommitments = ReadProperty<IEnumerable<GroupElement>>(reader, serializer, "BitCommitments");
	    reader.Read();
	    if (reader.TokenType == JsonToken.EndObject)
	    {
		    return ReflectionUtils.CreateInstance<IssuanceRequest>(new object[]{ ma, bitCommitments });
	    }

 		throw new ArgumentException($"No valid serialized {nameof(IssuanceRequest)} passed.");
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
 	public override void WriteJson(JsonWriter writer, IssuanceRequest? value, JsonSerializer serializer)
 	{
 		if (value is { } ir)
 		{
 			writer.WriteStartObject();
	        writer.WritePropertyName("Ma");
	        serializer.Serialize(writer, ir.Ma);
	        writer.WritePropertyName("BitCommitments");
	        serializer.Serialize(writer, ir.BitCommitments);
	        writer.WriteEndObject();
 			return;
 		}
 		throw new ArgumentException($"No valid {nameof(IssuanceRequest)}.", nameof(value));
 	}
 }
 