using ConcurrentCollections;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Store
{
	[JsonObject(MemberSerialization.OptIn)]
	public class CoinJoinStore
    {
		[JsonProperty(PropertyName = "Transactions")]
		public ConcurrentHashSet<CoinJoinTransaction> Transactions { get; private set; }

		public CoinJoinStore()
		{
			Transactions = new ConcurrentHashSet<CoinJoinTransaction>();
		}

		public async Task ToFileAsync(string filePath)
		{
			if (filePath == null) throw new ArgumentNullException(nameof(filePath));

			string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
			await File.WriteAllTextAsync(
				filePath,
				jsonString,
				Encoding.UTF8);
		}

		public async static Task<CoinJoinStore> CreateFromFileAsync(string filePath)
		{
			if (filePath == null) throw new ArgumentNullException(nameof(filePath));

			string jsonString = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
			return JsonConvert.DeserializeObject<CoinJoinStore>(jsonString);
		}
	}
}
