using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters
{
    public class GolombRiceFilterJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(GolombRiceFilter);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var value = Guard.Correct((string)reader.Value);

			var data = Encoders.Hex.DecodeData(value);
			return data.Length == 0 ? null : new GolombRiceFilter(data, 20, 1 << 20);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((GolombRiceFilter)value).ToString());
		}
	}
}
