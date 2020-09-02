using System;
using Newtonsoft.Json;
using WalletWasabi.Gui.Models;
using static WalletWasabi.Gui.Models.FeeDisplayFormat;

namespace WalletWasabi.Gui.Converters
{
	public class FeeDisplayFormatJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(FeeDisplayFormat);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			try
			{
				var value = reader.Value as string;

				if (string.IsNullOrWhiteSpace(value))
				{
					return SatoshiPerByte;
				}

				var displayFormatString = value.Trim();

				if (Enum.TryParse(displayFormatString, true, out FeeDisplayFormat displayFormat))
				{
					return displayFormat;
				}

				return SatoshiPerByte;
			}
			catch
			{
				return SatoshiPerByte;
			}
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((FeeDisplayFormat)value).ToString().ToLower());
		}
	}
}
