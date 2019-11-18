using System;

namespace WalletWasabi.JsonConverters
{
	public class FunnyBoolJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(bool);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			// check additional strings that are not checked by GetNetwork
			string canSpendUnconfirmedString = ((string)reader.Value).Trim();
			if ("true".Equals(canSpendUnconfirmedString, StringComparison.OrdinalIgnoreCase)
				|| "yes".Equals(canSpendUnconfirmedString, StringComparison.OrdinalIgnoreCase)
				|| "fuckyeah".Equals(canSpendUnconfirmedString, StringComparison.OrdinalIgnoreCase)
				|| canSpendUnconfirmedString == "1")
			{
				return true;
			}

			if ("false".Equals(canSpendUnconfirmedString, StringComparison.OrdinalIgnoreCase)
				|| "no".Equals(canSpendUnconfirmedString, StringComparison.OrdinalIgnoreCase)
				|| "nah".Equals(canSpendUnconfirmedString, StringComparison.OrdinalIgnoreCase)
				|| canSpendUnconfirmedString == "0")
			{
				return false;
			}

			return bool.Parse(canSpendUnconfirmedString);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((bool)value).ToString());
		}
	}
}
