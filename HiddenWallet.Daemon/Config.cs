using HiddenWallet.Converters;
using HiddenWallet.Crypto;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon
{
	[JsonObject(MemberSerialization.OptIn)]
	public class Config
    {
		[JsonProperty(PropertyName = "WalletFilePath", Order = 1)]
		public string WalletFilePath { get; private set; }

		[JsonProperty(PropertyName = "Network", Order = 2)]
		[JsonConverter(typeof(NetworkConverter))]
		public Network Network { get; private set; }

		[JsonProperty(PropertyName = "CanSpendUnconfirmed", Order = 3)]
		[JsonConverter(typeof(FunnyBoolConverter))]
		public bool? CanSpendUnconfirmed { get; set; }

		[JsonProperty(PropertyName = "ChaumianTumblerTestNetAddress", Order = 4)]
		public string ChaumianTumblerTestNetAddress { get; private set; }

		[JsonProperty(PropertyName = "ChaumianTumblerMainAddress", Order = 5)]
		public string ChaumianTumblerMainAddress { get; private set; }

		[JsonProperty(PropertyName = "ChaumianTumblerTestNetNotificationAddress", Order = 6)]
		public string ChaumianTumblerTestNetNotificationAddress { get; private set; }

		[JsonProperty(PropertyName = "ChaumianTumblerMainNotificationAddress", Order = 7)]
		public string ChaumianTumblerMainNotificationAddress { get; private set; }

		public Config()
		{

		}

		public async Task ToFileAsync(string path, CancellationToken cancel)
		{
			if (path == null) throw new ArgumentNullException(nameof(path));

			string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
			await File.WriteAllTextAsync(path,
			jsonString,
			Encoding.UTF8,
			cancel);
		}

		public async Task LoadOrCreateDefaultFileAsync(string path, CancellationToken cancel)
		{
			if (path == null) throw new ArgumentNullException(nameof(path));

			WalletFilePath = Path.Combine(FullSpvWallet.Global.DataDir, "Wallets", "Wallet.json");
			Network = Network.Main;
			CanSpendUnconfirmed = false;
			ChaumianTumblerTestNetAddress = "http://uitzert24go64ure.onion/";
			ChaumianTumblerMainAddress = "http://i4dmqjdw2nljsyt3.onion/"; // TODO: change it when active tumbler had been set up
			ChaumianTumblerTestNetNotificationAddress = "http://hiddenwallet.org:37126/";
			ChaumianTumblerMainNotificationAddress = "http://localhost:37126/"; // TODO: change it when active tumbler had been set up

			if (!File.Exists(path))
			{
				Console.WriteLine($"Config file did not exist. Created at path: {path}");
			}
			else
			{
				string jsonString = await File.ReadAllTextAsync(path, Encoding.UTF8, cancel);
				var config = JsonConvert.DeserializeObject<Config>(jsonString);

				WalletFilePath = config.WalletFilePath ?? WalletFilePath;
				Network = config.Network ?? Network;
				CanSpendUnconfirmed = config.CanSpendUnconfirmed ?? CanSpendUnconfirmed;
				ChaumianTumblerTestNetAddress = config.ChaumianTumblerTestNetAddress ?? ChaumianTumblerTestNetAddress;
				ChaumianTumblerMainAddress = config.ChaumianTumblerMainAddress ?? ChaumianTumblerMainAddress;
				ChaumianTumblerTestNetNotificationAddress = config.ChaumianTumblerTestNetNotificationAddress ?? ChaumianTumblerTestNetNotificationAddress;
				ChaumianTumblerMainNotificationAddress = config.ChaumianTumblerMainNotificationAddress ?? ChaumianTumblerMainNotificationAddress;
			}

			await ToFileAsync(path, cancel);
		}
	}
}
