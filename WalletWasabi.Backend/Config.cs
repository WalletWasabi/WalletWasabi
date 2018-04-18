using WalletWasabi.Converters;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Interfaces;

namespace WalletWasabi.Backend
{
	[JsonObject(MemberSerialization.OptIn)]
	public class Config : IConfig
	{
		/// <inheritdoc />
		public string FilePath { get; private set; }

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

		public Config(string filePath)
		{
			SetFilePath(filePath);
		}

		public Config(Network network, string bitcoinRpcUser, string bitcoinRpcPassword)
		{
			Network = Guard.NotNull(nameof(network), network);
			BitcoinRpcUser = Guard.NotNullOrEmptyOrWhitespace(nameof(bitcoinRpcUser), bitcoinRpcUser);
			BitcoinRpcPassword = Guard.NotNull(nameof(bitcoinRpcPassword), bitcoinRpcPassword);
		}

		/// <inheritdoc />
		public async Task ToFileAsync()
		{
			AssertFilePathSet();

			string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
			await File.WriteAllTextAsync(FilePath,
			jsonString,
			Encoding.UTF8);
		}

		/// <inheritdoc />
		public async Task LoadOrCreateDefaultFileAsync()
		{
			AssertFilePathSet();

			Network = Network.Main;
			BitcoinRpcUser = "user";
			BitcoinRpcPassword = "password";

			if (!File.Exists(FilePath))
			{
				Logger.LogInfo<Config>($"{nameof(Config)} file did not exist. Created at path: `{FilePath}`.");
			}
			else
			{
				string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
				var config = JsonConvert.DeserializeObject<Config>(jsonString);

				Network = config.Network ?? Network;
				BitcoinRpcUser = config.BitcoinRpcUser ?? BitcoinRpcUser;
				BitcoinRpcPassword = config.BitcoinRpcPassword ?? BitcoinRpcPassword;
			}

			await ToFileAsync();
		}

		/// <inheritdoc />
		public async Task<bool> CheckFileChangeAsync()
		{
			AssertFilePathSet();

			if (!File.Exists(FilePath))
			{
				throw new FileNotFoundException($"{nameof(Config)} file did not exist at path: `{FilePath}`.");
			}

			string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
			var config = JsonConvert.DeserializeObject<Config>(jsonString);

			if (Network != config.Network)
			{
				return true;
			}
			if (BitcoinRpcPassword != config.BitcoinRpcPassword)
			{
				return true;
			}
			if (BitcoinRpcUser != config.BitcoinRpcUser)
			{
				return true;
			}

			return false;
		}

		/// <inheritdoc />
		public void SetFilePath(string path)
		{
			FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(path), path, trim: true);
		}

		/// <inheritdoc />
		public void AssertFilePathSet()
		{
			if (FilePath == null) throw new NotSupportedException($"{nameof(FilePath)} is not set. Use {nameof(SetFilePath)} to set it.");
		}
	}
}
