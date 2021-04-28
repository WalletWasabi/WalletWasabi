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
				var value = reader.Value as string;

				// If minimized, then go with Maximized, because at start it shouldn't run with minimized.
				if (Enum.TryParse(value, out WindowState ws) && ws != WindowState.Minimized)
				{
					return ws.ToString();
				}
			}
			catch
			{
				// ignored
			}

			return WindowState.Maximized.ToString();
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(value);
		}
	}
}
