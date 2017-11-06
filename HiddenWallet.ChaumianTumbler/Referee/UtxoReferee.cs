using ConcurrentCollections;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Referee
{
	[JsonObject(MemberSerialization.OptIn)]
	public class UtxoReferee
    {
		/// <summary>
		/// Utxos banned for int minutes
		/// </summary>
		[JsonProperty(PropertyName = "Utxos")]
		public ConcurrentHashSet<OutPoint> Utxos { get; private set; }

		public UtxoReferee()
		{
			Utxos = new ConcurrentHashSet<OutPoint>();
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

		public async static Task<UtxoReferee> CreateFromFileAsync(string filePath)
		{
			if (filePath == null) throw new ArgumentNullException(nameof(filePath));

			string jsonString = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
			return JsonConvert.DeserializeObject<UtxoReferee>(jsonString);
		}
	}
}
