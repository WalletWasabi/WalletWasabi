using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace HiddenWallet.Converters
{
	/// <summary>
	/// Converter used to convert <see cref="DateTimeOffset"/> to and from Unix time.
	/// </summary>
	/// <seealso cref="Newtonsoft.Json.JsonConverter" />
	public class DateTimeOffsetConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(DateTimeOffset);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			return DateTimeOffset.FromUnixTimeSeconds(long.Parse((string)reader.Value));
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((DateTimeOffset)value).ToUnixTimeSeconds().ToString());
		}
	}
}
