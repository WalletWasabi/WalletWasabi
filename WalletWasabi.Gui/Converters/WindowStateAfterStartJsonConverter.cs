using Avalonia.Controls;
using Newtonsoft.Json;
using System;

namespace WalletWasabi.Gui.Converters
{
	public class WindowStateAfterStartJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(string);
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
					return WindowState.Maximized.ToString();
				}

				var windowStateString = value.Trim();

				return windowStateString.StartsWith("norm", StringComparison.OrdinalIgnoreCase)
					? WindowState.Normal.ToString()
					: WindowState.Maximized.ToString();
			}
			catch
			{
				return WindowState.Maximized.ToString();
			}
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(value);
		}
	}
}
