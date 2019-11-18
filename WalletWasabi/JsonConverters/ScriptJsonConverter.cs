using NBitcoin;

using System;

namespace WalletWasabi.JsonConverters
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
			var serialized = (string)reader.Value;

			return new Script(serialized);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var script = (Script)value;

			writer.WriteValue(script.ToString());
		}
	}
}
