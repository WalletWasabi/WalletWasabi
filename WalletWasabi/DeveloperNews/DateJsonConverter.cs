using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.DeveloperNews
{
	public class DateJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Date);
		}

		/// <inheritdoc />
		public override object? ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var dateString = reader.Value as string;
			if (string.IsNullOrWhiteSpace(dateString))
			{
				return null;
			}
			else
			{
				return Date.Parse(dateString);
			}
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var date = value as Date;

			writer.WriteValue(date?.ToString());
		}
	}
}
