using Newtonsoft.Json;
using System;
using WalletWasabi.Fluent.Models.Sorting;

namespace WalletWasabi.Fluent.Converters
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
				var readValue = (SortingPreference)serializer.Deserialize(reader, typeof(SortingPreference));
				return new SortingPreference(readValue.SortOrder, $"{readValue.ColumnTarget}SortDirection");
			}
			catch
			{
				return existingValue;
			}
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if (value is null)
			{
				return;
			}

			var saveValue = (SortingPreference)value;
			var newSaveValue = new SortingPreference(saveValue.SortOrder, saveValue.ColumnTarget.Replace("SortDirection", null));

			serializer.Serialize(writer, newSaveValue);
		}
	}
}
