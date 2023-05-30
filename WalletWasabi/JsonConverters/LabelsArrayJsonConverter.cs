using Newtonsoft.Json;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.JsonConverters;

public class LabelsArrayJsonConverter : JsonConverter<LabelsArray>
{
	/// <inheritdoc />
	public override LabelsArray ReadJson(JsonReader reader, Type objectType, LabelsArray existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			return new LabelsArray(serialized);
		}

		throw new ArgumentException($"No valid serialized {nameof(LabelsArray)} passed.");
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, LabelsArray value, JsonSerializer serializer)
	{
		var label = value;
		writer.WriteValue(label);
	}
}
