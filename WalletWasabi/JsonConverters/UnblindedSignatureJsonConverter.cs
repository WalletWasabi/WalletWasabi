using NBitcoin.BouncyCastle.Math;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using WalletWasabi.Crypto;

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

			var cs = new BigInteger(arr[0].Value<string>());
			var ss = new BigInteger(arr[1].Value<string>());

			var carr = cs.ToByteArrayUnsigned();
			var sarr = ss.ToByteArrayUnsigned();

			carr = new byte[32 - carr.Length].Concat(carr).ToArray();
			sarr = new byte[32 - sarr.Length].Concat(sarr).ToArray();
			var signatureBytes = carr.Concat(sarr).ToArray();
			var signature = ByteHelpers.ToHex(signatureBytes);

			var sig = UnblindedSignature.Parse(signature);
			return sig;
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var signature = ((UnblindedSignature)value).ToString();
			var c = new BigInteger(signature[..64], 16);
			var s = new BigInteger(signature[64..], 16);
			writer.WriteStartArray();
			writer.WriteValue(c.ToString());
			writer.WriteValue(s.ToString());
			writer.WriteEndArray();
		}
	}
}
