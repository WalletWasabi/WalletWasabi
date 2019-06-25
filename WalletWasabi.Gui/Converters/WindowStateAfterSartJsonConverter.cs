using Avalonia.Controls;
using Newtonsoft.Json;
using System;

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
				// If minimized, then go with Maximized, because at start it shouldn't run with minimized.
				var value = reader.Value as string;

				if (string.IsNullOrWhiteSpace(value))
				{
					return WindowState.Maximized;
				}

				if (value is null)
				{
					return WindowState.Maximized;
				}

				string windowStateString = value.Trim();
				if (WindowState.Normal.ToString().Equals(windowStateString, StringComparison.OrdinalIgnoreCase)
					|| "normal".Equals(windowStateString, StringComparison.OrdinalIgnoreCase)
					|| "norm".Equals(windowStateString, StringComparison.OrdinalIgnoreCase))
				{
					return WindowState.Normal;
				}
				else
				{
					return WindowState.Maximized;
				}
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
