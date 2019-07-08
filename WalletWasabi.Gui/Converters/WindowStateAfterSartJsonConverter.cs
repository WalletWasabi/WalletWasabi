using Avalonia.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Converters
{
	public class WindowStateAfterSartJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(WindowState);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			try
			{
				// If minimized, then go with Maximized, because at start it should not run with minimized.
				var value = reader.Value as string;

				if (string.IsNullOrWhiteSpace(value))
				{
					return WindowState.Maximized;
				}

				var windowStateString = value.Trim();

				return windowStateString.StartsWith("norm", StringComparison.OrdinalIgnoreCase)
					? WindowState.Normal
					: WindowState.Maximized;
			}
			catch
			{
				return WindowState.Maximized;
			}
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((WindowState)value).ToString());
		}
	}
}
