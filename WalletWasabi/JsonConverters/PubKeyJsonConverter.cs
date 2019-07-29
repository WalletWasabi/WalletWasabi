using NBitcoin;
using Newtonsoft.Json;
using System;

namespace WalletWasabi.JsonConverters
{
	public class PubKeyJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(PubKey);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			return new PubKey(((string)reader.Value).Trim());
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var pubKey = (PubKey)value;
			writer.WriteValue(pubKey.ToHex());
		}
	}
}
