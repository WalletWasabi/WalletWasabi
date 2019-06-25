using Newtonsoft.Json;
using System;
using WalletWasabi.Backend.Models;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters
{
	public class FilterModelJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(FilterModel);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var value = Guard.Correct((string)reader.Value);

			return string.IsNullOrWhiteSpace(value) ? default : FilterModel.FromFullLine(value);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((FilterModel)value).ToFullLine());
		}
	}
}
