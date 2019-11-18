using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters
{
	public class GolombRiceFilterJsonConverter : JsonConverter<GolombRiceFilter>
	{
		public override GolombRiceFilter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value =>
				{
					var data = Encoders.Hex.DecodeData(value);
					return new GolombRiceFilter(data, 20, 1 << 20);
				});

		public override void Write(Utf8JsonWriter writer, GolombRiceFilter value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString());
	}
}
