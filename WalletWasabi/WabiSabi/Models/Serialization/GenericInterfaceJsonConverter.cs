using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WalletWasabi.WabiSabi.Models.Serialization;

public class GenericInterfaceJsonConverter<T>(IEnumerable<Type> types) : JsonConverter<T>
{
	public override void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer)
	{
		if (value is { } nonNullableValue)
		{
			var stateTypeName = nonNullableValue.GetType().Name;
			var jObject = (JObject) JToken.FromObject(value, CreateJsonSerializer(serializer));
			jObject.AddFirst(new JProperty("Type", stateTypeName));
			jObject.WriteTo(writer);
		}
	}

	public override T? ReadJson(JsonReader reader, Type objectType, T? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var jsonObject = JObject.Load(reader);
		var typeName = jsonObject.Value<string>("Type");
		var stateType = types.Single(t => t.Name == typeName);
		return (T?)CreateJsonSerializer(serializer).Deserialize(jsonObject.CreateReader(), stateType);
	}

	private JsonSerializer CreateJsonSerializer(JsonSerializer serializer) =>
		JsonSerializer.Create(new JsonSerializerSettings
		{
			Converters = serializer.Converters.Where(x => x is not GenericInterfaceJsonConverter<T>).ToArray()
		});
}
