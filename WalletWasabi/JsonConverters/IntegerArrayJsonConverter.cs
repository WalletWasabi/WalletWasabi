using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.JsonConverters;

public class IntegerArrayJsonConverter : JsonConverter<IEnumerable<int>>
{
	public override IEnumerable<int>? ReadJson(JsonReader reader, Type objectType, IEnumerable<int>? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var stringValue = reader.Value as string;
		return Parse(stringValue);
	}

	public static IEnumerable<int>? Parse(string? stringValue)
	{
		if (stringValue is null)
		{
			return null;
		}
		else if (stringValue.Equals(string.Empty))
		{
			return Enumerable.Empty<int>();
		}

		return stringValue.Split(',').Select(strValue => int.Parse(strValue));
	}

	public override void WriteJson(JsonWriter writer, IEnumerable<int>? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			throw new ArgumentNullException(nameof(value));
		}
		var stringValue = string.Join(", ", value);
		writer.WriteValue(stringValue);
	}
}
