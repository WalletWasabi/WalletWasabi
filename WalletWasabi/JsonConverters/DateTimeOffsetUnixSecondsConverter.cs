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
			if (string.IsNullOrWhiteSpace(stringValue))
			{
				return default(DateTimeOffset);
			}
			else
			{
				return DateTimeOffset.FromUnixTimeSeconds(long.Parse(stringValue));
			}
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((DateTimeOffset)value).ToUnixTimeSeconds());
		}
	}
}
