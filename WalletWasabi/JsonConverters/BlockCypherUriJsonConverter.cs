using Newtonsoft.Json;
using System;

namespace WalletWasabi.JsonConverters
{
	internal class BlockCypherUriJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Uri);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var value = ((string)reader.Value).Trim();
			return new Uri(value, UriKind.Absolute);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((Uri)value).ToString());
		}
	}
}
