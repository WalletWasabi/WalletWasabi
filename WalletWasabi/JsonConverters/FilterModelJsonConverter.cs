using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Backend.Models;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters
{
	public class FilterModelJsonConverter : JsonConverter<FilterModel>
	{
		public override FilterModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var value = Guard.Correct(reader.GetString());
			return value.Length == 0 ? default : FilterModel.FromFullLine(value);
		}

		public override void Write(Utf8JsonWriter writer, FilterModel value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToFullLine());
		}
	}
}
