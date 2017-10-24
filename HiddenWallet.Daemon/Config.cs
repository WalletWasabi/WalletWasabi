using HiddenWallet.Converters;
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
		public bool CanSpendUnconfirmed { get; set; }

		public Config()
		{

		}

		public Config(string walletFilePath, Network network, bool canSpendUnconfirmed)
		{
			WalletFilePath = walletFilePath ?? throw new ArgumentNullException(nameof(walletFilePath));
			Network = network ?? throw new ArgumentNullException(nameof(network));
			CanSpendUnconfirmed = canSpendUnconfirmed;
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

		public static async Task<Config> CreateFromFileAsync(string path, CancellationToken cancel)
		{
			if (path == null) throw new ArgumentNullException(nameof(path));
			if (!File.Exists(path))
			{
				throw new ArgumentException($"Config file does not exist at {path}");
			}

			string jsonString = await File.ReadAllTextAsync(path, Encoding.UTF8, cancel);
			return JsonConvert.DeserializeObject<Config>(jsonString);
		}
    }
}
