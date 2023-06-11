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
		reader.Expect(JsonToken.StartObject);
		var ma = reader.ReadProperty<GroupElement>(serializer, "Ma");
		var bitCommitments = reader.ReadProperty<IEnumerable<GroupElement>>(serializer, "BitCommitments");
		reader.Read();
		reader.Expect(JsonToken.EndObject);
		return ReflectionUtils.CreateInstance<IssuanceRequest>(new object[] { ma, bitCommitments });
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, IssuanceRequest? ir, JsonSerializer serializer)
	{
		if (ir is null)
		{
			throw new ArgumentException($"No valid {nameof(IssuanceRequest)}.", nameof(ir));
		}
		writer.WriteStartObject();
		writer.WriteProperty("Ma", ir.Ma, serializer);
		writer.WriteProperty("BitCommitments", ir.BitCommitments, serializer);
		writer.WriteEndObject();
	}
}
