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

			return new UnblindedSignature(new BigInteger(c), new BigInteger(s));
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			UnblindedSignature signature = (UnblindedSignature)value;
			writer.WriteStartArray();
			writer.WriteValue(signature.C.ToString());
			writer.WriteValue(signature.S.ToString());
			writer.WriteEndArray();
		}
	}
}
