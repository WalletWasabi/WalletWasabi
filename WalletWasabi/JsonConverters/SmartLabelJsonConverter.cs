using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.BlockchainAnalysis;

namespace WalletWasabi.JsonConverters
{
	public class SmartLabelJsonConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(SmartLabel);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var s = reader.Value as string;
			return new SmartLabel(s);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var label = value as SmartLabel;
			writer.WriteValue(label?.ToString() ?? "");
		}
	}
}
