using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Models;

namespace WalletWasabi.JsonConverters;

public class CoinjoinSkipFactorsJsonConverter : JsonConverter<CoinjoinSkipFactors>
{
	/// <inheritdoc />
	public override CoinjoinSkipFactors? ReadJson(JsonReader reader, Type objectType, CoinjoinSkipFactors? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			return CoinjoinSkipFactors.FromString(serialized);
		}
		throw new ArgumentException($"No valid serialized {nameof(CoinjoinSkipFactors)} passed.");
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, CoinjoinSkipFactors? value, JsonSerializer serializer)
	{
		string str = value?.ToString() ?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(str);
	}
}
