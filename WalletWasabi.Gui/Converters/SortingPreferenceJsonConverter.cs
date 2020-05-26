using Newtonsoft.Json;
using System;
using WalletWasabi.Gui.Controls;

namespace WalletWasabi.Gui.Converters
{
	public class SortingPreferenceJsonConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(SortingPreference);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			try
			{
				return serializer.Deserialize(reader, typeof(SortingPreference));
			}
			catch
			{
				return existingValue;
			}
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			serializer.Serialize(writer, value);
		}
	}
}
