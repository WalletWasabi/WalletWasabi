using Newtonsoft.Json;
using System;

namespace WalletWasabi.JsonConverters
{
	public class TimeSpanSecondsConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(TimeSpan);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var seconds = (long)reader.Value;
			return TimeSpan.FromSeconds(seconds);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var timeSpan = (TimeSpan)value;
			writer.WriteValue((long)timeSpan.TotalSeconds);
		}
	}
}
