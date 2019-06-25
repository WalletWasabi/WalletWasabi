using NBitcoin.BouncyCastle.Math;
using Newtonsoft.Json;
using System;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters
{
    public class BigIntegerJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(BigInteger);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var value = Guard.Correct((string)reader.Value);

			return string.IsNullOrWhiteSpace(value) ? default : new BigInteger(value);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((BigInteger)value).ToString());
		}
	}
}
