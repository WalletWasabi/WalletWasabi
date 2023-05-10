using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using WalletWasabi.Extensions;

namespace WalletWasabi.JsonConverters;

internal class OutPointWithDateConverter : JsonConverter<Dictionary<OutPoint, DateTimeOffset>>
{
	public override Dictionary<OutPoint, DateTimeOffset> ReadJson(JsonReader reader, Type objectType, Dictionary<OutPoint, DateTimeOffset>? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		Dictionary<OutPoint, DateTimeOffset> dic = new();
		if (reader.Value is string serialized)
		{
			var op = new OutPoint();
			var date = DateTimeOffset.MinValue;
			var splitValueArray = serialized.Split(":::");
			op.FromHex(splitValueArray[0]);
			date = DateTimeOffset.Parse(splitValueArray[1]);
		}
		return dic;
		throw new ArgumentException($"No valid serialized {nameof(OutPoint)} passed.");
	}

	public override void WriteJson(JsonWriter writer, Dictionary<OutPoint, DateTimeOffset>? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			throw new ArgumentNullException(nameof(value));
		}
		foreach (var pair in value)
		{
			string opHex = pair.Key?.ToHex() ?? throw new ArgumentNullException(nameof(pair.Key));
			string date = pair.Value.ToString() ?? throw new ArgumentNullException(nameof(pair.Value));
			writer.WriteValue($"{opHex}:::{date}");
		}
	}
}
