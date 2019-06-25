using NBitcoin;
using Newtonsoft.Json;
using System;

namespace WalletWasabi.JsonConverters
{
    public class KeyPathJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(KeyPath);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var s = (string)reader.Value;
			if (string.IsNullOrWhiteSpace(s))
			{
				return null;
			}
			var kp = KeyPath.Parse(s.Trim());

			return kp;
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var kp = (KeyPath)value;

			var s = kp.ToString();
			writer.WriteValue(s);
		}
	}
}
