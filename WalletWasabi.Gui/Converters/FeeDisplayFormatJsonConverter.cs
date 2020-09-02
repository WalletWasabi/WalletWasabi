using Avalonia.Data.Converters;
using System;
using System.Globalization;
using WalletWasabi.Gui.Models;
using WalletWasabi.Exceptions;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace WalletWasabi.Gui.Converters
{
	public class FeeDisplayFormatJsonConverter : JsonConverter
	{
		private Dictionary<string, FeeDisplayFormat> Texts { get; } = new Dictionary<string, FeeDisplayFormat>
		{
			{ "satoshiperbyte", FeeDisplayFormat.SatoshiPerByte },
			{ "usd", FeeDisplayFormat.USD },
			{ "btc", FeeDisplayFormat.BTC },
			{ "percentage", FeeDisplayFormat.Percentage },
		};

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
					return FeeDisplayFormat.SatoshiPerByte;
				}

				var displayFormatString = value.Trim().ToLower();

				return Texts[displayFormatString];
			}
			catch
			{
				return FeeDisplayFormat.SatoshiPerByte;
			}
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((FeeDisplayFormat)value).ToString());
		}
	}
}
