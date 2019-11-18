using NBitcoin;

using System;

namespace WalletWasabi.JsonConverters
{
	public class OutPointJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(OutPoint);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var value = (string)reader.Value;
			var op = new OutPoint();
			op.FromHex(value);
			return op;
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			string opHex = ((OutPoint)value).ToHex();
			writer.WriteValue(opHex);
		}
	}
}
