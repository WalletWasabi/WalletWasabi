using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Math;
using System;
using WalletWasabi.Crypto;

namespace WalletWasabi.JsonConverters
{
	public class RsaPubKeyJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(BlindingRsaPubKey);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			JObject o = JObject.Load(reader);
			BigInteger modulus = new BigInteger(o["modulus"].Value<string>());
			BigInteger exponent = new BigInteger(o["exponent"].Value<string>());
			return new BlindingRsaPubKey(modulus, exponent);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var pubKey = (BlindingRsaPubKey)value;
			JObject o = new JObject();

			o.Add(new JProperty("modulus", pubKey.Modulus.ToString()));
			o.Add(new JProperty("exponent", pubKey.Exponent.ToString()));

			o.WriteTo(writer);
		}
	}
}
