using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace WalletWasabi.JsonConverters
{
	public class UnblindedSignatureJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(UnblindedSignature);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			JArray arr = JArray.Load(reader);

			string c = arr[0].Value<string>();
			string s = arr[1].Value<string>();

			return UnblindedSignature.Parse($"{c}{s}");
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var signature = ((UnblindedSignature)value).ToString();
			writer.WriteStartArray();
			writer.WriteValue(signature[..64]);
			writer.WriteValue(signature[64..]);
			writer.WriteEndArray();
		}
	}
}
