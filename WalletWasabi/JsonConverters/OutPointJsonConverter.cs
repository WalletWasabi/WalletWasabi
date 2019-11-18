using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class OutPointJsonConverter : JsonConverter<OutPoint>
	{
		public override OutPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value =>
			{
				var op = new OutPoint();
				op.FromHex(value);
				return op;
			});

		public override void Write(Utf8JsonWriter writer, OutPoint value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToHex());
	}
}
