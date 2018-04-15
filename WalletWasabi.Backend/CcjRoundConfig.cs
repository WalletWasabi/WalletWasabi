using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Converters;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Backend
{
	[JsonObject(MemberSerialization.OptIn)]
	public class CcjRoundConfig
    {
		[JsonProperty(PropertyName = "Denomination")]
		[JsonConverter(typeof(MoneyConverter))]
		public Money Denomination { get; private set; }

		public CcjRoundConfig()
		{

		}

		public CcjRoundConfig(Money denomination)
		{
			Denomination = Guard.NotNull(nameof(denomination), denomination);
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

			Denomination = new Money(0.1m, MoneyUnit.BTC);

			if (!File.Exists(path))
			{
				Logger.LogInfo<CcjRoundConfig>($"{nameof(CcjRoundConfig)} file did not exist. Created at path: `{path}`.");
			}
			else
			{
				string jsonString = await File.ReadAllTextAsync(path, Encoding.UTF8);
				var config = JsonConvert.DeserializeObject<CcjRoundConfig>(jsonString);

				Denomination = config.Denomination ?? Denomination;
			}

			await ToFileAsync(path);
		}

		public async Task<bool> CheckFileChangeAsync(string path)
		{
			if (!File.Exists(path))
			{
				throw new FileNotFoundException($"{nameof(CcjRoundConfig)} file did not exist at path: `{path}`.");
			}

			string jsonString = await File.ReadAllTextAsync(path, Encoding.UTF8);
			var config = JsonConvert.DeserializeObject<CcjRoundConfig>(jsonString);

			if(Denomination != config.Denomination)
			{
				return true;
			}

			return false;
		}
	}
}
