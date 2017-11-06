using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace HiddenWallet.Converters
{
	public class OutPointConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(OutPoint);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var hex = ((string)reader.Value).Trim();
			var op = new OutPoint();
			op.FromHex(hex);
			return op;
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((OutPoint)value).ToHex());
		}
	}
}
