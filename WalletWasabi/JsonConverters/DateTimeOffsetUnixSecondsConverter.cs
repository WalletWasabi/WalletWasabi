using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WalletWasabi.JsonConverters
{
	public class DateTimeOffsetUnixSecondsConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(DateTimeOffset);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var stringValue = reader.Value as string;
			long value;
			if (string.IsNullOrWhiteSpace(stringValue))
			{
				value = default(DateTimeOffset).ToUnixTimeSeconds();
			}
			else
			{
				value = long.Parse(stringValue);
			}
			return DateTimeOffset.FromUnixTimeSeconds(value);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((DateTimeOffset)value).ToUnixTimeSeconds());
		}
	}
}
