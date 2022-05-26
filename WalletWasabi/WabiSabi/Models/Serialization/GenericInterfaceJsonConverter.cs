using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WalletWasabi.WabiSabi.Models.Serialization;

public class GenericInterfaceJsonConverter<T> : JsonConverter<T>
{
	// This converter is a bit unusual because we need to add a new property to the
	// serialized json string but the converter is called recursively and fails with
	// a "Self referencing loop" exception.
	// The workaround is detect it and prevent reentering by setting CanRead and
	// CanWrite to false immediately after entering.
	// see: https://github.com/JamesNK/Newtonsoft.Json/issues/386
	[ThreadStatic]
	private static bool IsReading;

	[ThreadStatic]
	private static bool IsWriting;

	public GenericInterfaceJsonConverter(IEnumerable<Type> types)
	{
		Types = types;
	}

	public override bool CanWrite => !IsWriting;

	public override bool CanRead => !IsReading;

	public IEnumerable<Type> Types { get; }

	public override void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer)
	{
		try
		{
			IsWriting = true;
			if (value is not null)
			{
				var stateTypeName = value.GetType().Name;
				var jObject = (JObject)JToken.FromObject(value, serializer);
				jObject.AddFirst(new JProperty("Type", stateTypeName));
				jObject.WriteTo(writer);
			}
		}
		finally
		{
			IsWriting = false;
		}
	}

	public override T? ReadJson(JsonReader reader, Type objectType, T? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		try
		{
			IsReading = true;

			var jsonObject = JObject.Load(reader);
			var typeName = jsonObject.Value<string>("Type");
			var stateType = Types.Single(t => t.Name == typeName);
			return (T?)serializer.Deserialize(jsonObject.CreateReader(), stateType);
		}
		finally
		{
			IsReading = false;
		}
	}
}
