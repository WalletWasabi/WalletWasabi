using MagicalCryptoWallet.Converters;
using MagicalCryptoWallet.Logging;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagicalCryptoWallet.Backend
{
	[JsonObject(MemberSerialization.OptIn)]
	public class Config
    {
		[JsonProperty(PropertyName = "Network")]
		[JsonConverter(typeof(NetworkConverter))]
		public Network Network { get; private set; }

		[JsonProperty(PropertyName = "BitcoinRpcUser")]
		public string BitcoinRpcUser { get; private set; }

		[JsonProperty(PropertyName = "BitcoinRpcPassword")]
		public string BitcoinRpcPassword { get; private set; }

		public Config()
		{

		}

		public async Task ToFileAsync(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(nameof(path));

			string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
			await File.WriteAllTextAsync(path,
			jsonString,
			Encoding.UTF8);
		}

		public async Task LoadOrCreateDefaultFileAsync(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(nameof(path));

			Network = Network.Main;
			BitcoinRpcUser = "user";
			BitcoinRpcPassword = "password";

			if (!File.Exists(path))
			{
				Logger.LogInfo<Config>($"Config file did not exist. Created at path: `{path}`.");
			}
			else
			{
				string jsonString = await File.ReadAllTextAsync(path, Encoding.UTF8);
				var config = JsonConvert.DeserializeObject<Config>(jsonString);

				Network = config.Network ?? Network;
				BitcoinRpcUser = config.BitcoinRpcUser ?? BitcoinRpcUser;
				BitcoinRpcPassword = config.BitcoinRpcPassword ?? BitcoinRpcPassword;
			}

			await ToFileAsync(path);
		}
	}
}
