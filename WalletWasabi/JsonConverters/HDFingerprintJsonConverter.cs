using NBitcoin;
using Newtonsoft.Json;
using System;

namespace WalletWasabi.JsonConverters
{
	public class HDFingerprintJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(HDFingerprint);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var s = (string)reader.Value;
			if (string.IsNullOrWhiteSpace(s))
			{
				return null;
			}

			var fp = new HDFingerprint(ByteHelpers.FromHex(s));
			return fp;
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var fp = (HDFingerprint)value;

			var s = fp.ToString();
			writer.WriteValue(s);
		}
	}
}
