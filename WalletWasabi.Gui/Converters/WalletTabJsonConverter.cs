using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Converters
{
	internal class WalletTabJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(WalletTab);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			try
			{
				var value = reader.Value as string;
				if (string.IsNullOrEmpty(value))
				{
					return WalletTab.CoinJoin;
				}

				if (value == WalletTab.Send.ToString())
				{
					return WalletTab.Send;
				}
				else if (value == WalletTab.Receive.ToString())
				{
					return WalletTab.Receive;
				}
				else if (value == WalletTab.History.ToString())
				{
					return WalletTab.History;
				}
				else if (value == WalletTab.Build.ToString())
				{
					return WalletTab.Build;
				}
				else
				{
					return WalletTab.CoinJoin;
				}
			}
			catch
			{
				return WalletTab.CoinJoin;
			}
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((WalletTab)value).ToString());
		}
	}
}
