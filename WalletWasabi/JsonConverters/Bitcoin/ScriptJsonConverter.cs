using System;
using System.ComponentModel;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters.Bitcoin
{
	public class ScriptJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Script);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var stringValue = reader.Value as string;
			return Parse(stringValue);
		}

		public static Script Parse(string? stringValue)
		{
			return new Script(stringValue);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var v = (Script)value;
			writer.WriteValue(v.ToString());
		}
	}
}
