using NBitcoin;
using Newtonsoft.Json;
using System;
using NBitcoin.DataEncoders;

namespace WalletWasabi.JsonConverters
{
	public class ExtPubKeyJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(ExtPubKey);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var hex = (string)reader.Value;
			return new ExtPubKey(Encoders.Hex.DecodeData(hex));
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var epk = (ExtPubKey)value;
			var hex = Encoders.Hex.EncodeData(epk.ToBytes());
			writer.WriteValue(hex);
		}
	}
}
