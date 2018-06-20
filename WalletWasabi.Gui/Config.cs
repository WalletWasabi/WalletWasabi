using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui
{
	[JsonObject(MemberSerialization.OptIn)]
	public class Config : IConfig
	{
		/// <inheritdoc />
		public string FilePath { get; private set; }

		[JsonProperty(PropertyName = "Network")]
		[JsonConverter(typeof(NetworkJsonConverter))]
		public Network Network { get; private set; }

		[JsonProperty(PropertyName = "TestNetBackendUri")]
		public string TestNetBackendUri { get; private set; }

		[JsonProperty(PropertyName = "MainNetBackendUri")]
		public string MainNetBackendUri { get; private set; }

		public Uri GetCurrentUri() => Network == Network.Main ? new Uri(MainNetBackendUri) : new Uri(TestNetBackendUri);

		public Config()
		{
		}

		public Config(string filePath)
		{
			SetFilePath(filePath);
		}

		public Config(Network network, string testNetBackendUri, string mainNetBackendUri)
		{
			Network = Guard.NotNull(nameof(network), network);
			TestNetBackendUri = Guard.NotNullOrEmptyOrWhitespace(nameof(testNetBackendUri), testNetBackendUri);
			MainNetBackendUri = Guard.NotNullOrEmptyOrWhitespace(nameof(mainNetBackendUri), mainNetBackendUri);
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
			TestNetBackendUri = "http://wtgjmaol3io5ijii.onion/";
			MainNetBackendUri = "http://4jsmnfcsmbrlm7l7.onion/";

			if (!File.Exists(FilePath))
			{
				Logger.LogInfo<Config>($"{nameof(Config)} file did not exist. Created at path: `{FilePath}`.");
			}
			else
			{
				string jsonString = await File.ReadAllTextAsync(FilePath, Encoding.UTF8);
				var config = JsonConvert.DeserializeObject<Config>(jsonString);

				Network = config.Network ?? Network;
				TestNetBackendUri = config.TestNetBackendUri ?? TestNetBackendUri;
				MainNetBackendUri = config.MainNetBackendUri ?? MainNetBackendUri;
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

			if (!TestNetBackendUri.Equals(config.TestNetBackendUri, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (!MainNetBackendUri.Equals(config.MainNetBackendUri, StringComparison.OrdinalIgnoreCase))
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
