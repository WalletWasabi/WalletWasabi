using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.JsonConverters
{
	public class SmartLabelJsonConverter : JsonConverter<SmartLabel>
	{
		public override SmartLabel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => new SmartLabel(value));

		public override void Write(Utf8JsonWriter writer, SmartLabel value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value ?? "");
	}
}
