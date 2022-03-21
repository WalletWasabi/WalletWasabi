using Newtonsoft.Json;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.JsonConverters;

public class SmartLabelJsonConverter : JsonConverter<SmartLabel>
{
	/// <inheritdoc />
	public override SmartLabel? ReadJson(JsonReader reader, Type objectType, SmartLabel? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var s = reader.Value as string;
		return new SmartLabel(s);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, SmartLabel? value, JsonSerializer serializer)
	{
		var label = value;
		writer.WriteValue(label ?? "");
	}
}
