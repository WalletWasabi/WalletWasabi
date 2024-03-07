using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.JsonConverters.Collections;

public class HashSetUint256JsonConverter : JsonConverter<HashSet<uint256>>
{
	public override HashSet<uint256>? ReadJson(JsonReader reader, Type objectType, HashSet<uint256>? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		JToken token = JToken.Load(reader);
		if (token.Type == JTokenType.Array)
		{
			var value = new HashSet<uint256>();
			var set = token.ToObject<HashSet<string>>();
			foreach (var item in set)
			{
				value.Add(new(item));
			}
			return value;
		}

		return new HashSet<uint256>();
	}

	public override void WriteJson(JsonWriter writer, HashSet<uint256>? value, JsonSerializer serializer)
	{
		if (value == null)
		{
			writer.WriteNull();
			return;
		}

		writer.WriteStartArray();
		foreach (var item in value)
		{
			writer.WriteValue(item.ToString());
		}

		writer.WriteEndArray();
	}
}
