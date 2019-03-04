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
				// If minimized, then go with Maximized, because at start it shouldn't run with minimized.
				if (reader.Value is null)
				{
					return WindowState.Maximized;
				}

				string windowStateString = ((string)reader.Value).Trim();
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
